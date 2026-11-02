using System;
using System.Collections;

namespace ObservableCollections.Tests;

/// <summary>
/// Position-based operations on a writable view (the setter, RemoveAt, and Insert) must translate the
/// view index back to the source index (AlternateIndexList.GetAlternateIndex) before applying them to
/// the source.
///
/// T and TView are different types so that the shortcut that skips the converter
/// (typeof(T) == typeof(TView)) does not interfere.
/// </summary>
public class WritableViewIndexTranslationTest
{
    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        return int.Parse(newView.Substring(1));
    }

    /// <summary>
    /// Verifies that RemoveAt on a filtered writable view removes the element the caller sees at the index, by
    /// translating the index in the view into the index in the source.
    /// </summary>
    [Fact]
    public void RemoveAt()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"]. Its [1] is "$4", so 4 should be removed from the source.
        bindable.RemoveAt(1);

        list.Should().Equal(new[] { 1, 2, 3 });

        bindable.Count.Should().Be(1);
        bindable[0].Should().Be("$2");
    }

    /// <summary>
    /// Verifies that Insert on a filtered writable view inserts right before the element the caller sees at the
    /// index, by translating the index in the view into the index in the source.
    /// </summary>
    [Fact]
    public void Insert()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"]. We want to insert at its [1], so in the source
        // it should go right before 4 (source index 3).
        bindable.Insert(1, "$6");

        list.Should().Equal(new[] { 1, 2, 3, 6, 4 });

        bindable.Count.Should().Be(3);
        bindable[0].Should().Be("$2");
        bindable[1].Should().Be("$6");
        bindable[2].Should().Be("$4");
    }

    /// <summary>
    /// An insertion at the tail has no corresponding source index, so it is treated as an append.
    /// This is a legitimate operation and must not be confused with rejecting an out-of-range index.
    /// </summary>
    [Fact]
    public void InsertAtTail()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"]. Its [2] is the tail.
        bindable.Insert(2, "$6");

        list.Should().Equal(new[] { 1, 2, 3, 4, 6 });

        bindable.Should().Equal(new[] { "$2", "$4", "$6" });
    }

    /// <summary>
    /// Verifies that an out-of-range index is rejected with ArgumentOutOfRangeException and leaves the
    /// source unchanged.
    /// The fallback to appending used to swallow out-of-range indices as well.
    /// </summary>
    [Fact]
    public void OutOfRangeIndexIsRejected()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"], so Count is 2.
        bindable.Invoking(x => x.Insert(3, "$6")).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.Insert(-1, "$6")).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.RemoveAt(2)).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.RemoveAt(-1)).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x[2] = "$6").Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x[-1] = "$6").Should().Throw<ArgumentOutOfRangeException>();

        list.Should().Equal(new[] { 1, 2, 3, 4 });
    }

    /// <summary>
    /// Verifies that IList.Add on a filtered writable view returns -1 for an element that the filter excludes, and
    /// the index in the view for one that it includes.
    /// Count does not change for an excluded element, so Count - 1 used to return the index of another element.
    /// </summary>
    [Fact]
    public void IListAddOfExcludedElement()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2"]. Count - 1 was 0, the index of "$2".
        ((IList)bindable).Add("$3").Should().Be(-1);

        ((IList)bindable).Add("$4").Should().Be(1);

        list.Should().Equal(new[] { 1, 2, 3, 4 });
        bindable.Should().Equal(new[] { "$2", "$4" });
    }

    /// <summary>
    /// Verifies that the setter on a filtered view replaces the element the caller saw at the index, when the converter
    /// rejects the source write and the filter changes while the converter runs.
    /// A filter change does not take the lock of the source, so another thread can make it at that point; the converter
    /// makes it here so that it surely happens there. The position translated before the converter was reused after it,
    /// and the setter replaced the element that had moved into that position.
    /// </summary>
    [Fact]
    public void RejectedSetWhileFilterChanges()
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged((string newView, int original, ref bool setValue) =>
        {
            view.ResetFilter();
            setValue = false;
            return original;
        });

        // The view is ["$2", "$4"], so its [1] is 4. After the reset, 4 is at [3], and [1] is 2.
        bindable[1] = "$40";

        bindable.Should().Equal(new[] { "$1", "$2", "$3", "$40" });
    }

    /// <summary>
    /// Verifies that the setter on a filtered view does not replace another element, when the converter rejects the
    /// source write and the new filter that is attached while the converter runs excludes the target. The assigned view
    /// must still be kept, and appear once the filter includes the element again.
    /// See RejectedSetWhileFilterChanges for why the converter changes the filter.
    /// </summary>
    [Fact]
    public void RejectedSetWhileFilterExcludesTarget()
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged((string newView, int original, ref bool setValue) =>
        {
            view.AttachFilter(x => x % 2 == 1);
            setValue = false;
            return original;
        });

        // The view is ["$2", "$4"], so its [1] is 4. The new filter excludes 4, and puts 3 at [1].
        bindable[1] = "$40";

        bindable.Should().Equal(new[] { "$1", "$3" });

        view.ResetFilter();

        bindable.Should().Equal(new[] { "$1", "$2", "$3", "$40" });
    }
}

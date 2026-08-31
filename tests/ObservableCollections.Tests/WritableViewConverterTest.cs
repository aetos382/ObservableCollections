namespace ObservableCollections.Tests;

/// <summary>
/// 書き込み可能ビューの Add / Insert / Remove は
/// typeof(T) == typeof(TView) のとき converter をスキップして値をそのままソースに渡す。
/// 型が同じことと変換が不要なことは別なので、transform が恒等写像でなければソースが壊れる。
/// セッターにはこのショートカットが無く、必ず converter を通す。
///
/// ソースは大文字、View は小文字。ユーザーは View で見た小文字で操作する。
/// </summary>
public class WritableViewConverterTest
{
    static string ToOriginal(string newView, string original, ref bool setValue)
    {
        return newView.ToUpperInvariant();
    }

    [Fact]
    public void Add()
    {
        var list = new ObservableList<string>();
        list.Add("ALPHA");

        using var view = list.CreateWritableView(x => x.ToLowerInvariant());
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        bindable.Add("beta");

        list.Should().Equal(new[] { "ALPHA", "BETA" });

        // View 側は transform が再適用されるのでどちらでも小文字に見える。
        // つまりこの破壊はユーザーの目には映らない。
        bindable.Count.Should().Be(2);
        bindable[1].Should().Be("beta");
    }

    [Fact]
    public void Insert()
    {
        var list = new ObservableList<string>();
        list.Add("ALPHA");

        using var view = list.CreateWritableView(x => x.ToLowerInvariant());
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        bindable.Insert(0, "beta");

        list.Should().Equal(new[] { "BETA", "ALPHA" });
    }

    [Fact]
    public void Remove()
    {
        var list = new ObservableList<string>();
        list.Add("ALPHA");
        list.Add("BETA");

        using var view = list.CreateWritableView(x => x.ToLowerInvariant());
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // 小文字のままソースを検索するので見つからず、黙って何も消えない。
        bindable.Remove("beta").Should().BeTrue();

        list.Should().Equal(new[] { "ALPHA" });
    }

    /// <summary>
    /// 比較用。セッターにはショートカットが無いので converter が呼ばれる。
    /// 同じクラスの中で一貫していないことを示す。
    /// </summary>
    [Fact]
    public void Set()
    {
        var list = new ObservableList<string>();
        list.Add("ALPHA");

        using var view = list.CreateWritableView(x => x.ToLowerInvariant());
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        bindable[0] = "beta";

        list.Should().Equal(new[] { "BETA" });
    }
}

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ObservableCollections.Tests
{
    public class ReentrancyTest
    {
        /// <summary>
        /// CollectionChanged ハンドラ内からコレクションを変更した場合、後続の購読者に対する通知順序を
        /// 保証できないため、その変更が拒否され、呼び出し元に CollectionReentrancyException が届くことを確認する。
        /// </summary>
        [Fact]
        public void MutatingFromHandler_Throws()
        {
            var list = new ObservableList<int>();

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    list.Clear();
                }
            };

            var act = () => list.Add(12);

            act.Should().Throw<CollectionReentrancyException>();
        }

        /// <summary>
        /// 再入が拒否される際、入れ子の変更がコレクションに適用されないことを確認する。
        /// 適用してしまうと、通知されない変更が残ってしまう。
        /// </summary>
        [Fact]
        public void MutatingFromHandler_DoesNotApplyTheNestedChange()
        {
            var list = new ObservableList<int>();

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    list.Clear();
                }
            };

            try
            {
                list.Add(12);
            }
            catch (CollectionReentrancyException)
            {
            }

            list.Should().Equal(12);
        }

        /// <summary>
        /// 再入が拒否されても通知の配送は中断されず、違反したハンドラより後に登録された購読者にも通知が
        /// 届くことを確認する。ルールを守っている購読者が、他の購読者の違反によってソースとずれてはならない。
        /// </summary>
        [Fact]
        public void MutatingFromHandler_StillNotifiesLaterSubscribers()
        {
            var list = new ObservableList<int>();

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    list.Clear();
                }
            };

            var view = list.CreateView(x => new ViewContainer<int>(x));

            try
            {
                list.Add(12);
            }
            catch (CollectionReentrancyException)
            {
            }

            list.Should().Equal(12);
            view.Select(x => x.Value).Should().Equal(12);
        }

        /// <summary>
        /// 拒否はハンドラに対して例外を投げずに行われるが、違反そのものは配送完了後に呼び出し元へ報告される
        /// ことを確認する。ハンドラ側で例外を握り潰す購読者 (R3 など) 経由でも違反が隠れてはならない。
        /// </summary>
        [Fact]
        public void MutatingFromHandler_IsReportedToTheCallerWithoutThrowingInTheHandler()
        {
            var list = new ObservableList<int>();
            var handlerSawException = false;

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (e.Action != NotifyCollectionChangedAction.Add)
                {
                    return;
                }

                try
                {
                    list.Clear();
                }
                catch (Exception)
                {
                    handlerSawException = true;
                }
            };

            var act = () => list.Add(12);

            act.Should().Throw<CollectionReentrancyException>();
            handlerSawException.Should().BeFalse();
        }

        /// <summary>
        /// 拒否を戻り値で表現できるメソッドでは、拒否されたことが戻り値に現れることを確認する。
        /// </summary>
        [Fact]
        public void MutatingFromHandler_ReturnsFalseFromTheRejectedCall()
        {
            var list = new ObservableList<int>(new[] { 1, 2 });
            var removeResult = default(bool?);

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    removeResult = list.Remove(1);
                }
            };

            try
            {
                list.Add(3);
            }
            catch (CollectionReentrancyException)
            {
            }

            removeResult.Should().BeFalse();
            list.Should().Equal(1, 2, 3);
        }

        /// <summary>
        /// 戻り値の型に「拒否」を表す値が存在しないメソッドでは、default を返して型の約束を破るのではなく
        /// 即座に例外を投げることを確認する。この場合に限り通知の配送は中断される。
        /// </summary>
        [Fact]
        public void MutatingFromHandler_ThrowsImmediatelyFromValueReturningMethods()
        {
            var queue = new ObservableQueue<int>(new[] { 1 });
            var laterSubscriberNotified = false;

            queue.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    queue.Dequeue();
                }
            };

            queue.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                laterSubscriberNotified = true;
            };

            var act = () => queue.Enqueue(2);

            act.Should().Throw<CollectionReentrancyException>();
            laterSubscriberNotified.Should().BeFalse();
            queue.Should().Equal(1, 2);
        }

        /// <summary>
        /// ハンドラが例外を投げた場合でも再入の検出状態が解除され、以降の変更が拒否されないことを確認する。
        /// </summary>
        [Fact]
        public void HandlerThrows_SubsequentChangesAreNotRejected()
        {
            var list = new ObservableList<int>();
            var throwFromHandler = true;

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                if (throwFromHandler)
                {
                    throw new InvalidTimeZoneException();
                }
            };

            try
            {
                list.Add(12);
            }
            catch (InvalidTimeZoneException)
            {
            }

            throwFromHandler = false;

            var act = () => list.Add(34);

            act.Should().NotThrow();
            list.Should().Equal(12, 34);
        }

        /// <summary>
        /// 再入の検出はコレクション単位であり、通知中に別のスレッドから行われた変更は SyncRoot で
        /// 待たされた上で通常の変更として適用され、誤って拒否されないことを確認する。
        /// </summary>
        [Fact]
        public void MutatingFromAnotherThreadWhileNotifying_IsNotRejected()
        {
            var list = new ObservableList<int>();
            var handlerEntered = new ManualResetEventSlim();
            var fromAnotherThread = default(Exception);

            list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
            {
                handlerEntered.Set();
            };

            var another = Task.Run(() =>
            {
                handlerEntered.Wait();

                try
                {
                    list.Add(99);
                }
                catch (Exception ex)
                {
                    fromAnotherThread = ex;
                }
            });

            list.Add(12);
            another.Wait();

            fromAnotherThread.Should().BeNull();
            list.Should().BeEquivalentTo(new[] { 12, 99 });
        }
    }
}

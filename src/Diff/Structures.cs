//#define USE_HASH_TABLE

using System;
using System.Diagnostics;

namespace Xnlab.SQLMon.Diff
{
    public interface IDiffList {

        int Count();

        IComparable GetByIndex(int index);
    }

    internal enum DiffStatus {
        Matched = 1,
        NoMatch = -1,
        Unknown = -2
    }

    internal class DiffState {
        private const int BadIndex = -1;
        private int _length;

        public DiffState() {
            SetToUnkown();
        }

        public int StartIndex { get; private set; }
        public int EndIndex => StartIndex + _length - 1;

        public int Length {
            get {
                int len;
                if (_length > 0) {
                    len = _length;
                }
                else {
                    if (_length == 0)
                        len = 1;
                    else
                        len = 0;
                }

                return len;
            }
        }

        public DiffStatus Status {
            get {
                DiffStatus stat;
                if (_length > 0)
                    stat = DiffStatus.Matched;
                else
                    switch (_length) {
                        case -1:
                            stat = DiffStatus.NoMatch;
                            break;

                        default:
                            Debug.Assert(_length == -2, "Invalid status: _length < -2");
                            stat = DiffStatus.Unknown;
                            break;
                    }
                return stat;
            }
        }

        protected void SetToUnkown() {
            StartIndex = BadIndex;
            _length = (int) DiffStatus.Unknown;
        }

        public void SetMatch(int start, int length) {
            Debug.Assert(length > 0, "Length must be greater than zero");
            Debug.Assert(start >= 0, "Start must be greater than or equal to zero");
            StartIndex = start;
            _length = length;
        }

        public void SetNoMatch() {
            StartIndex = BadIndex;
            _length = (int) DiffStatus.NoMatch;
        }

        public bool HasValidLength(int newStart, int newEnd, int maxPossibleDestLength) {
            if (_length > 0) //have unlocked match
                if (maxPossibleDestLength < _length || StartIndex < newStart || EndIndex > newEnd)
                    SetToUnkown();
            return _length != (int) DiffStatus.Unknown;
        }
    }

    internal class DiffStateList {
#if USE_HASH_TABLE
		private Hashtable _table;
#else
        private readonly DiffState[] _array;
#endif

        public DiffStateList(int destCount) {
#if USE_HASH_TABLE
			_table = new Hashtable(Math.Max(9,destCount/10));
#else
            _array = new DiffState[destCount];
#endif
        }

        public DiffState GetByIndex(int index) {
#if USE_HASH_TABLE
			DiffState retval = (DiffState)_table[index];
			if (retval == null)
			{
				retval = new DiffState();
				_table.Add(index,retval);
			}
#else
            var retval = _array[index];
            if (retval == null) {
                retval = new DiffState();
                _array[index] = retval;
            }
#endif
            return retval;
        }
    }

    public enum DiffResultSpanStatus {
        NoChange,
        Replace,
        DeleteSource,
        AddDestination
    }

    public class DiffResultSpan : IComparable {
        private const int BadIndex = -1;

        protected DiffResultSpan(
            DiffResultSpanStatus status,
            int destIndex,
            int sourceIndex,
            int length) {
            Status = status;
            DestIndex = destIndex;
            SourceIndex = sourceIndex;
            Length = length;
        }

        public int DestIndex { get; }
        public int SourceIndex { get; }
        public int Length { get; private set; }
        public DiffResultSpanStatus Status { get; }

        public static DiffResultSpan CreateNoChange(int destIndex, int sourceIndex, int length) {
            return new DiffResultSpan(DiffResultSpanStatus.NoChange, destIndex, sourceIndex, length);
        }

        public static DiffResultSpan CreateReplace(int destIndex, int sourceIndex, int length) {
            return new DiffResultSpan(DiffResultSpanStatus.Replace, destIndex, sourceIndex, length);
        }

        public static DiffResultSpan CreateDeleteSource(int sourceIndex, int length) {
            return new DiffResultSpan(DiffResultSpanStatus.DeleteSource, BadIndex, sourceIndex, length);
        }

        public static DiffResultSpan CreateAddDestination(int destIndex, int length) {
            return new DiffResultSpan(DiffResultSpanStatus.AddDestination, destIndex, BadIndex, length);
        }

        public void AddLength(int i) {
            Length += i;
        }

        public override string ToString() {
            return string.Format("{0} (Dest: {1},Source: {2}) {3}",
                Status.ToString(),
                DestIndex.ToString(),
                SourceIndex.ToString(),
                Length.ToString());
        }

        #region IComparable Members

        public int CompareTo(object obj) {
            return DestIndex.CompareTo(((DiffResultSpan) obj).DestIndex);
        }

        #endregion IComparable Members
    }
}
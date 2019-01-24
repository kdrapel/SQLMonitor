namespace Xnlab.SQLMon.Common
{
    public class KeyValue<TK, TV> {

        public KeyValue(TK key, TV value) {
            Key = key;
            Value = value;
        }

        public TK Key { get; set; }
        public TV Value { get; set; }
    }
}
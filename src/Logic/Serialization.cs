using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace Xnlab.SQLMon.Logic
{
    #region Serialization

    //http://codebetter.com/blogs/gregyoung/archive/2008/08/24/fast-serialization.aspx
    internal class CustomBinaryFormatter : IFormatter {
        private const int SizeLength = 8;
        private const int ChunckSize = 5000;
        private readonly Dictionary<int, Type> _mById = new Dictionary<int, Type>();
        private readonly Dictionary<Type, int> _mByType = new Dictionary<Type, int>();
        private readonly byte[] _mCopyBuffer;
        private readonly byte[] _mLengthBuffer = new byte[SizeLength];
        private readonly BinaryReader _mReader;
        private readonly MemoryStream _mReadStream;
        private readonly BinaryWriter _mWriter;
        private readonly MemoryStream _mWriteStream;
        private MemoryStream _mIndexWriteStream;
        private long _pending;

        public CustomBinaryFormatter(Stream indexStream)
            : this(indexStream, null) {
        }

        public CustomBinaryFormatter(Stream indexStream, Stream serializationStream) {
            _mCopyBuffer = new byte[SizeLength * 1000000];
            _mWriteStream = new MemoryStream(100000);
            _mReadStream = new MemoryStream(100000);
            _mWriter = new BinaryWriter(_mWriteStream);
            _mReader = new BinaryReader(_mReadStream);
            _mIndexWriteStream = new MemoryStream(ChunckSize * SizeLength);
            IndexStream = indexStream;
            DataStream = serializationStream;
            if (indexStream != null) {
                bool indexReady;
                if (indexStream.Length >= SizeLength)
                    if (indexStream.Read(_mLengthBuffer, 0, SizeLength) == SizeLength) {
                        Count = BitConverter.ToInt64(_mLengthBuffer, 0);
                        indexReady = true;
                    }
                    else {
                        indexReady = false;
                    }
                else
                    indexReady = false;

                if (!indexReady) {
                    indexStream.Position = 0;
                    indexStream.Write(BitConverter.GetBytes(0L), 0, SizeLength);
                }

                indexStream.Seek(indexStream.Length, SeekOrigin.Begin);
            }
        }

        public Stream DataStream { get; }

        public Stream IndexStream { get; }

        public long Count { get; private set; }

        public object Deserialize(Stream serializationStream) {
            return null;
        }

        public void Serialize(Stream serializationStream, object graph) {
        }

        public ISurrogateSelector SurrogateSelector { get; set; }

        public SerializationBinder Binder { get; set; }

        public StreamingContext Context { get; set; }

        ~CustomBinaryFormatter() {
            Close();
        }

        public void Flush() {
            if (IndexStream != null) {
                if (_pending % ChunckSize != 0) {
                    var buffer = _mIndexWriteStream.ToArray();
                    IndexStream.Write(buffer, 0, buffer.Length);
                    _mIndexWriteStream = new MemoryStream(ChunckSize * SizeLength);
                    _pending = 0;
                }

                IndexStream.Position = 0;
                IndexStream.Write(BitConverter.GetBytes(Count), 0, SizeLength);
            }
        }

        public void Close() {
            if (IndexStream != null)
                IndexStream.Close();
            if (DataStream != null)
                DataStream.Close();
        }

        public void Register<T>(int typeId) where T : ICustomBinarySerializable {
            _mById.Add(typeId, typeof(T));
            _mByType.Add(typeof(T), typeId);
        }

        public void MoveTo(long index) {
            MoveTo(index, true);
        }

        public void MoveTo(long index, bool relocate) {
            if (IndexStream != null && DataStream != null)
                if (index >= 0 && index * SizeLength <= IndexStream.Length - SizeLength) {
                    var pos = IndexStream.Position;
                    IndexStream.Position = SizeLength + index * SizeLength;
                    if (IndexStream.Read(_mLengthBuffer, 0, SizeLength) == SizeLength)
                        DataStream.Seek(BitConverter.ToInt64(_mLengthBuffer, 0), SeekOrigin.Begin);
                    if (relocate)
                        IndexStream.Position = pos;
                }
        }

        public void MoveToEnd() {
            IndexStream.Seek(0, SeekOrigin.End);
            DataStream.Seek(0, SeekOrigin.End);
        }

        public T Deserialize<T>(bool full) {
            if (DataStream.Read(_mLengthBuffer, 0, SizeLength) != SizeLength)
                //throw new SerializationException("Could not read length from the stream.");
                return default;
            var length = BitConverter.ToInt32(_mLengthBuffer, 0);
            //TODO make this support partial reads from stream
            if (DataStream.Read(_mCopyBuffer, 0, length) != length)
                throw new SerializationException("Could not read " + length + " bytes from the stream.");
            _mReadStream.Seek(0L, SeekOrigin.Begin);
            _mReadStream.Write(_mCopyBuffer, 0, length);
            _mReadStream.Seek(0L, SeekOrigin.Begin);
            var typeid = _mReader.ReadInt32();
            Type t;
            if (!_mById.TryGetValue(typeid, out t))
                throw new SerializationException("TypeId " + typeid + " is not a registerred type id");
            var obj = FormatterServices.GetUninitializedObject(t);
            var deserialize = (ICustomBinarySerializable) obj;
            deserialize.SetDataFrom(_mReader, full);
            if (_mReadStream.Position != length)
                throw new SerializationException("object of type " + t +
                                                 " did not read its entire buffer during deserialization. This is most likely an inbalance between the writes and the reads of the object.");
            return (T) deserialize;
        }

        public void Serialize<T>(T graph) {
            Serialize(graph, false);
        }

        public void Serialize<T>(T graph, bool isUpdate) {
            int key;
            if (!_mByType.TryGetValue(graph.GetType(), out key))
                throw new SerializationException(graph.GetType() + " has not been registered with the serializer");
            var c = (ICustomBinarySerializable) graph; //this will always work due to generic constraint on the Register
            _mWriteStream.Seek(0L, SeekOrigin.Begin);
            _mWriter.Write(key);
            c.WriteDataTo(_mWriter);
            if (IndexStream != null && !isUpdate) {
                Count++;
                _pending++;
                if (_pending % ChunckSize == 0) {
                    var buffer = _mIndexWriteStream.ToArray();
                    IndexStream.Write(buffer, 0, buffer.Length);
                    _mIndexWriteStream = new MemoryStream(ChunckSize * SizeLength);
                    _pending = 0;
                }

                _mIndexWriteStream.Write(BitConverter.GetBytes(DataStream.Position), 0, SizeLength);
            }

            DataStream.Write(BitConverter.GetBytes(_mWriteStream.Position), 0, SizeLength);
            DataStream.Write(_mWriteStream.GetBuffer(), 0, (int) _mWriteStream.Position);
        }
    }

    public interface ICustomBinarySerializable {

        void WriteDataTo(BinaryWriter writer);

        void SetDataFrom(BinaryReader reader, bool full);
    }

    #endregion Serialization
}
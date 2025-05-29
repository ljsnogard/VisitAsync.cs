namespace SampleGenVisit.NsSerdes
{
    using Cysharp.Threading.Tasks;

    using NsAbsVisitAsync;

    using NsManualGenVisit;

    public class MsgPackSerdesError
    { }

    public class MsgPackPrimSerializer :
        IVisitor<bool>,
        IVisitor<byte>, IVisitor<sbyte>,
        IVisitor<ushort>, IVisitor<short>,
        IVisitor<uint>, IVisitor<int>,
        IVisitor<ulong>, IVisitor<long>,
        IVisitor<char>, IVisitor<string>,
        IVisitor<float>, IVisitor<double>, IVisitor<decimal>
    {
        public UniTask<bool> VisitAsync(bool data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(byte data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(sbyte data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(ushort data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(short data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(uint data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(int data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(ulong data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(long data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(char data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(string data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(float data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(double data, CancellationToken token = default)
            => throw new NotImplementedException();
        public UniTask<bool> VisitAsync(decimal data, CancellationToken token = default)
            => throw new NotImplementedException();
        public void Dispose()
            => throw new NotImplementedException();
    }

    public sealed class MsgPackSerializer<T> : MsgPackPrimSerializer, IVisitor<T>, IVisitorProvider
    {
        private UniTask<bool> TryMatchVisit(T data, CancellationToken token = default)
        {
            return data switch
            {
                bool b => base.VisitAsync(b, token),
                byte u8 => base.VisitAsync(u8, token),
                sbyte i8 => base.VisitAsync(i8, token),
                ushort u16 => base.VisitAsync(u16, token),
                short i16 => base.VisitAsync(i16, token),
                uint u32 => base.VisitAsync(u32, token),
                int i32 => base.VisitAsync(i32, token),
                ulong u64 => base.VisitAsync(u64, token),
                long i64 => base.VisitAsync(i64, token),
                char ch => base.VisitAsync(ch, token),
                string str => base.VisitAsync(str, token),
                float f32 => base.VisitAsync(f32, token),
                double f64 => base.VisitAsync(f64, token),
                decimal f128 => base.VisitAsync(f128, token),
                _ => UniTask.FromResult(false),
            };
        }

        public async UniTask<bool> VisitAsync(T data, CancellationToken token = default)
        {
            var tryMatch = await this.TryMatchVisit(data, token);
            if (tryMatch)
                return true;
            var optRecept = await ReceptionistInject.GetAsync<T>(token);
            if (!optRecept.IsSome(out var receptionist))
                return false;
            return await receptionist.ReceptAsync(data, this, this, token);
        }

        public UniTask<IVisitor<U>> GetMemberVisitorAsync<P, U>(
            IVisitor<P> parent,
            uint remainingMemberCount,
            string key,
            CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public UniTask<IVisitor<U>> GetVariantVisitorAsync<P, U>(
            IVisitor<P> parent,
            uint remainingVariantCount,
            CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }
}
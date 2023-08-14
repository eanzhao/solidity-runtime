using NBitcoin;
using NBitcoin.DataEncoders;

namespace SolidityWasmRuntime;

public interface IContext
{
    Dictionary<string, byte[]> Storage { get; set; }
    void SetStorage(byte[] key, byte[] value, bool takeOld);
    bool TryGetStorage(byte[] key, out byte[] value);
}

public class Context : IContext
{
    public Dictionary<string, byte[]> Storage { get; set; } = new();

    public void SetStorage(byte[] key, byte[] value, bool takeOld)
    {
        Storage[Encoders.Base58.EncodeData(key)] = value;
    }

    public bool TryGetStorage(byte[] key, out byte[] value)
    {
        var keyStr = Encoders.Base58.EncodeData(key);
        if (Storage.ContainsKey(keyStr))
        {
            if (Storage.TryGetValue(keyStr, out value!))
            {
                return true;
            }
        }

        value = Array.Empty<byte>();
        return false;
    }
}
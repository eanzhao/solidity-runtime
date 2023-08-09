using Wasmtime;

namespace SolidityWasmRuntime;

public class Runtime : IDisposable
{
    private readonly Store _store;
    private readonly Engine _engine;
    private readonly Linker _linker;
    private readonly Memory _memory;
    private readonly Module _module;
    public byte[] ReturnBuffer = Array.Empty<byte>();
    public readonly Dictionary<int, int> Database = new();
    public byte[] Input { get; set; } = Array.Empty<byte>();

    public Runtime(byte[] wasmCode)
    {
        _engine = new Engine(new Config().WithFuelConsumption(true));
        _store = new Store(_engine);
        _linker = new Linker(_engine);
        _memory = new Memory(_store, 16, 16);
        _module = Module.FromBytes(_engine, "contract", wasmCode);
        DefineImportFunctions();
    }

    public Runtime(string wasmFilePath)
    {
        _engine = new Engine(new Config().WithFuelConsumption(true));
        _store = new Store(_engine);
        _linker = new Linker(_engine);
        _memory = new Memory(_store, 16, 16);
        _module = Module.FromTextFile(_engine, wasmFilePath);
        DefineImportFunctions();
    }

    public Instance Instantiate()
    {
        return _linker.Instantiate(_store, _module);
    }

    public void AddFuel(ulong fuel)
    {
        _store.AddFuel(fuel);
    }

    public void PrintConsumedFuel()
    {
        Console.WriteLine($"Consumed {_store.GetConsumedFuel()} units of fuel.");
    }

    private void DefineImportFunctions()
    {
        _linker.Define("env", "memory", _memory);
        _linker.DefineFunction("seal0", "input", (Action<int, int>)InputFunc);
        _linker.DefineFunction("seal0", "seal_return", (Action<int, int, int>)SealReturn);
        _linker.DefineFunction("seal0", "debug_message", (Func<int, int, int>)DebugMessage);
        _linker.DefineFunction("seal0", "value_transferred", (Action<int, int>)ValueTransferred);
        _linker.DefineFunction("seal2", "set_storage", (Func<int, int, int, int, int>)SetStorage);
        _linker.DefineFunction("seal1", "get_storage", (Func<int, int, int, int, int>)GetStorage);
    }

    #region API functions

    private void InputFunc(int dataPtr, int dataLenPtr)
    {
        WriteBytes(dataPtr, Input);
        WriteUInt32(dataLenPtr, Convert.ToUInt32(Input.Length));
    }

    private void SealReturn(int flags, int dataPtr, int dataLen)
    {
        Console.WriteLine($"SealReturn: {flags}, {dataPtr}, {dataLen}");
        ReturnBuffer = new byte[dataLen];
        for (var offset = dataLen - 1; offset >= 0; offset--)
        {
            ReturnBuffer[offset] = _memory.ReadByte(dataPtr + offset);
        }
    }

    private int DebugMessage(int outPtr, int outLenPtr)
    {
        Console.WriteLine($"ValueTransferred: {outPtr}, {outLenPtr}");
        Console.WriteLine($"DebugMessage: {outPtr}, {outLenPtr}");
        return 0;
    }

    private int GetStorage(int keyPtr, int keyLen, int outPtr, int outLenPtr)
    {
        Console.WriteLine($"GetStorage: {keyPtr}, {keyLen}, {outPtr}, {outLenPtr}");
        if (Database.TryGetValue(keyPtr, out var result))
        {
            return result;
        }

        return 0;
    }

    private void ValueTransferred(int valuePtr, int valueLengthPtr)
    {
        WriteUInt32(valueLengthPtr, 0);
    }

    private int SetStorage(int keyPtr, int keyLen, int valuePtr, int valueLen)
    {
        Console.WriteLine($"SetStorage: {keyPtr}, {keyLen}, {valuePtr}, {valueLen}");
        if (Database.TryAdd(keyPtr, valuePtr))
        {
            return 0;
        }

        var preValue = Database[keyPtr];
        Database[keyPtr] = valuePtr;
        return preValue;
    }

    #endregion

    #region Helper functions

    private void WriteBytes(int address, byte[] data)
    {
        foreach (var (offset, byt) in data.Select((b, i) => (i, b)))
        {
            _memory.Write(address + offset, byt);
        }
    }

    private void WriteUInt32(int address, uint value)
    {
        var numberInBytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(numberInBytes);
        }

        WriteBytes(address, numberInBytes);
    }

    #endregion

    void IDisposable.Dispose()
    {
        _module.Dispose();
        _linker.Dispose();
        _store.Dispose();
        _engine.Dispose();
    }
}
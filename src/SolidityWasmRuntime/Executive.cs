using NBitcoin.DataEncoders;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Solang;
using Wasmtime;

namespace SolidityWasmRuntime;

public class Executive
{
    private readonly Runtime _runtime;

    public Executive(string solFilePath)
    {
        var solidityCode = File.ReadAllText(solFilePath);
        var output = new Compiler().BuildWasm(solidityCode);
        var wasmCode = output.Contracts.First().WasmCode.ToByteArray();
        _runtime = new Runtime(new Context(), wasmCode);
    }

    public string Execute(string selector, params int[] values)
    {
        var abiEncode = new ABIEncode();
        var parameters = !values.Any()
            ? string.Empty
            : abiEncode.GetABIEncoded(new ABIValue("uint256", values[0])).ToHex();
        _runtime.Input = Encoders.Hex.DecodeData(selector + parameters);
        var instance = _runtime.Instantiate();
        var call = instance.GetAction("call");
        if (call is null)
        {
            Console.WriteLine("error: call export is missing");
            return string.Empty;
        }

        _runtime.AddFuel(2000);

        try
        {
            call.Invoke();
        }
        catch (TrapException ex)
        {
            //Console.WriteLine("got exception " + ex.Message);
        }

        _runtime.PrintConsumedFuel();
        var hexReturn = Convert.ToHexString(_runtime.ReturnBuffer);
        Console.WriteLine();
        return hexReturn;
    }
}
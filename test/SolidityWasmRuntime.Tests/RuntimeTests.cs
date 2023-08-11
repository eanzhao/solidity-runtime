using NBitcoin.DataEncoders;
using Shouldly;

namespace SolidityWasmRuntime;

public class RuntimeTests
{
    [Fact]
    public void SealReturnWithSuccessStatus()
    {
        const string watFilePath = "watFiles/code_return_with_data.wat";
        var runtime = new Runtime(watFilePath, false, 1, 1);
        runtime.Input = Encoders.Hex.DecodeData("00000000445566778899");
        var instance = runtime.Instantiate();
        InvokeCall(instance.GetAction("call"));
        var hexReturn = Convert.ToHexString(runtime.ReturnBuffer);
        hexReturn.ShouldBe("445566778899");
    }

    [Fact]
    public void DebugMessageWorks()
    {
        const string watFilePath = "watFiles/code_debug_message.wat";
        var runtime = new Runtime(watFilePath, false, 1, 1);
        var instance = runtime.Instantiate();
        InvokeCall(instance.GetAction("call"));
        runtime.DebugMessages.Count.ShouldBe(1);
        runtime.DebugMessages.First().ShouldBe("Hello World!");
    }

    private void InvokeCall(Action? call)
    {
        try
        {
            call?.Invoke();
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
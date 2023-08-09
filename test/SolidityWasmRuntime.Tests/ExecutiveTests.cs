using Epoche;
using Shouldly;

namespace SolidityWasmRuntime;

public class ExecutiveTests
{
    [Fact]
    public void IsPowerOf2Test()
    {
        const string solFilePath = "simple.sol";
        const string functionName = "is_power_of_2(uint256)";
        var executive = new Executive(solFilePath);
        {
            var result = executive.Execute(Keccak256.ComputeEthereumFunctionSelector(functionName, false),
                1023);
            result.ShouldBe("00");
        }
        {
            var result = executive.Execute(Keccak256.ComputeEthereumFunctionSelector(functionName, false),
                1024);
            result.ShouldBe("01");
        }
    }
}
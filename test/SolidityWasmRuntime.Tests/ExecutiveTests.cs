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

    [Fact]
    public void FooTest()
    {
        const string solFilePath = "simple.sol";
        const string functionName = "foo()";
        var executive = new Executive(solFilePath);
        var result = executive.Execute(Keccak256.ComputeEthereumFunctionSelector(functionName, false));
        result.ShouldNotBeNull();
    }

    [Fact(Skip = "get_storage / set_storage not implemented.")]
    public void BarTest()
    {
        FooTest();
        
        const string solFilePath = "simple.sol";
        const string functionName = "bar()";
        var executive = new Executive(solFilePath);
        var result = executive.Execute(Keccak256.ComputeEthereumFunctionSelector(functionName, false));
        result.ShouldNotBeNull();
    }
}
contract simple {
    uint32 number;

    function foo() public returns (uint32) {
        number = 2;
        return number;
    }
    
    function bar() public view returns (uint32) {
        return number;
    }

    function is_power_of_2(uint n) pure public returns (bool) {
        return n != 0 && (n & (n - 1)) == 0;
    }
}
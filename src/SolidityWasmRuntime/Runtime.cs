using System.Text;
using NBitcoin.DataEncoders;
using Nethereum.Hex.HexConvertors.Extensions;
using Wasmtime;

namespace SolidityWasmRuntime;

public class Runtime : IDisposable
{
    private readonly IContext _context;
    private readonly Store _store;
    private readonly Engine _engine;
    private readonly Linker _linker;
    private readonly Memory _memory;
    private readonly Module _module;
    public byte[] ReturnBuffer = Array.Empty<byte>();
    public readonly List<string> DebugMessages = new();
    public byte[] Input { get; set; } = Array.Empty<byte>();

    public Runtime(IContext context, byte[] wasmCode, bool withFuelConsumption = true, long memoryMin = 16, long memoryMax = 16)
    {
        _context = context;
        _engine = new Engine(new Config().WithFuelConsumption(withFuelConsumption));
        _store = new Store(_engine);
        _linker = new Linker(_engine);
        _memory = new Memory(_store, memoryMin, memoryMax);
        _module = Module.FromBytes(_engine, "contract", wasmCode);
        DefineImportFunctions();
    }

    public Runtime(IContext context, string watFilePath, bool withFuelConsumption = true, long memoryMin = 16, long memoryMax = 16)
    {
        _context = context;
        _engine = new Engine(new Config().WithFuelConsumption(withFuelConsumption));
        _store = new Store(_engine);
        _linker = new Linker(_engine);
        _memory = new Memory(_store, memoryMin, memoryMax);
        _module = Module.FromTextFile(_engine, watFilePath);
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

        _linker.DefineFunction("seal0", "set_storage", (Action<int, int, int>)SetStorageV0);
        _linker.DefineFunction("seal1", "set_storage", (Func<int, int, int, int>)SetStorageV1);
        _linker.DefineFunction("seal2", "set_storage", (Func<int, int, int, int, int>)SetStorageV2);

        _linker.DefineFunction("seal0", "clear_storage", (Action<int>)ClearStorageV0);
        _linker.DefineFunction("seal1", "clear_storage", (Func<int, int, int>)ClearStorageV1);

        _linker.DefineFunction("seal0", "get_storage", (Func<int, int, int, int>)GetStorageV0);
        _linker.DefineFunction("seal1", "get_storage", (Func<int, int, int, int, int>)GetStorageV1);

        _linker.DefineFunction("seal0", "contains_storage", (Func<int, int>)ContainsStorageV0);
        _linker.DefineFunction("seal1", "contains_storage", (Func<int, int, int>)ContainsStorageV1);

        _linker.DefineFunction("seal0", "take_storage", (Func<int, int, int, int, int>)TakeStorageV0);

        _linker.DefineFunction("seal0", "transfer", (Func<int, int, int, int, int>)TransferV0);

        _linker.DefineFunction("seal0", "call", (Func<int, int, long, int, int, int, int, int, int, int>)CallV0);
        _linker.DefineFunction("seal1", "call", (Func<int, int, long, int, int, int, int, int, int, int>)CallV1);
        _linker.DefineFunction("seal2", "call", (Func<int, int, long, long, int, int, int, int, int, int, int>)CallV2);

        _linker.DefineFunction("seal0", "delegate_call", (Func<int, int, int, int, int, int, int>)DelegateCallV0);

        _linker.DefineFunction("seal0", "instantiate", (caller, args, results) =>
            {
                InstantiateV0(args[0].AsInt32(), args[1].AsInt32(), args[2].AsInt64(), args[3].AsInt32(),
                    args[4].AsInt32(), args[5].AsInt32(), args[6].AsInt32(), args[7].AsInt32(), args[8].AsInt32(),
                    args[9].AsInt32(), args[10].AsInt32(), args[11].AsInt32(), args[12].AsInt32());
            },
            new[]
            {
                ValueKind.Int32, ValueKind.Int32, ValueKind.Int64, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32,
                ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32,
                ValueKind.Int32
            }, new List<ValueKind> { ValueKind.Int32 });
        _linker.DefineFunction("seal1", "instantiate",
            (Func<int, long, int, int, int, int, int, int, int, int, int, int>)InstantiateV1);
        _linker.DefineFunction("seal2", "instantiate", (caller, args, results) =>
            {
                InstantiateV2(args[0].AsInt32(), args[1].AsInt64(), args[2].AsInt64(), args[3].AsInt32(),
                    args[4].AsInt32(), args[5].AsInt32(), args[6].AsInt32(), args[7].AsInt32(), args[8].AsInt32(),
                    args[9].AsInt32(), args[10].AsInt32(), args[11].AsInt32(), args[12].AsInt32());
            },
            new[]
            {
                ValueKind.Int32, ValueKind.Int64, ValueKind.Int64, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32,
                ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32,
                ValueKind.Int32
            }, new List<ValueKind> { ValueKind.Int32 });

        _linker.DefineFunction("seal0", "terminate", (Action<int, int>)TerminateV0);
        _linker.DefineFunction("seal1", "terminate", (Action<int>)TerminateV1);

        _linker.DefineFunction("seal0", "seal_input", (Action<int, int>)SealInputV0);

        _linker.DefineFunction("seal0", "seal_return", (Action<int, int, int>)SealReturnV0);

        _linker.DefineFunction("seal0", "caller", (Action<int, int>)Caller);

        _linker.DefineFunction("seal0", "is_contract", (Func<int, int>)IsContract);

        _linker.DefineFunction("seal0", "code_hash", (Func<int, int, int, int>)CodeHash);

        _linker.DefineFunction("seal0", "own_code_hash", (Func<int, int, int>)OwnCodeHash);

        _linker.DefineFunction("seal0", "caller_is_origin", CallerIsOrigin);

        _linker.DefineFunction("seal0", "caller_is_root", CallerIsRoot);

        _linker.DefineFunction("seal0", "address", (Action<int, int>)Address);

        _linker.DefineFunction("seal0", "weight_to_fee", (Action<long, int, int>)WeightToFeeV0);
        _linker.DefineFunction("seal1", "weight_to_fee", (Action<long, long, int, int>)WeightToFeeV1);

        _linker.DefineFunction("seal0", "gas_left", (Action<int, int>)GasLeftV0);
        _linker.DefineFunction("seal1", "gas_left", (Action<int, int>)GasLeftV1);

        _linker.DefineFunction("seal0", "balance", (Action<int, int>)Balance);

        _linker.DefineFunction("seal0", "value_transferred", (Action<int, int>)ValueTransferred);

        _linker.DefineFunction("seal0", "random", (Func<int, int, int, int, int>)RandomV0);
        _linker.DefineFunction("seal1", "random", (Func<int, int, int, int, int>)RandomV1);

        _linker.DefineFunction("seal0", "now", (Action<int, int>)Now);

        _linker.DefineFunction("seal0", "minimum_balance", (Action<int, int>)MinimumBalance);

        _linker.DefineFunction("seal0", "deposit_event", (Action<int, int, int, int>)DepositEvent);

        _linker.DefineFunction("seal0", "block_number", (Action<int, int>)BlockNumber);

        _linker.DefineFunction("seal0", "hash_sha2_256", (Action<int, int, int>)HashSha2_256);
        _linker.DefineFunction("seal0", "hash_keccak_256", (Action<int, int, int>)HashKeccak256);
        _linker.DefineFunction("seal0", "hash_blake2_256", (Action<int, int, int>)HashBlake2_256);
        _linker.DefineFunction("seal0", "hash_blake2_128", (Action<int, int, int>)HashBlake2_128);

        _linker.DefineFunction("seal0", "call_chain_extension", (Action<int, int, int, int, int>)CallChainExtension);

        _linker.DefineFunction("seal0", "seal_debug_message", (Func<int, int, int>)SealDebugMessage);

        _linker.DefineFunction("seal0", "call_runtime", (Func<int, int, int>)CallRuntime);

        _linker.DefineFunction("seal0", "ecdsa_recover", (Func<int, int, int, int>)EcdsaRecover);

        _linker.DefineFunction("seal0", "sr25519_verify", (Func<int, int, int, int, int>)Sr25519Verify);

        _linker.DefineFunction("seal0", "set_code_hash", (Func<int, int>)SetCodeHash);

        _linker.DefineFunction("seal0", "ecdsa_to_eth_address", (Func<int, int, int>)EcdsaToEthAddress);

        _linker.DefineFunction("seal0", "reentrance_count", ReentranceCount);
        _linker.DefineFunction("seal0", "account_reentrance_count", (Func<int, int>)AccountReentranceCount);

        _linker.DefineFunction("seal0", "instantiation_nonce", InstantiationNonce);

        _linker.DefineFunction("seal0", "add_delegate_dependency", (Action<int>)AddDelegateDependency);
        _linker.DefineFunction("seal0", "remove_delegate_dependency", (Action<int>)RemoveDelegateDependency);
    }

    #region API functions

    /// <summary>
    /// Set the value at the given key in the contract storage.
    ///
    /// Equivalent to the newer [`seal1`][`super::api_doc::Version1::set_storage`] version with the
    /// exception of the return type. Still a valid thing to call when not interested in the return
    /// value.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the location to store the value is placed.</param>
    /// <param name="valuePtr">pointer into the linear memory where the value to set is placed.</param>
    /// <param name="valueLen">the length of the value in bytes.</param>
    private void SetStorageV0(int keyPtr, int valuePtr, int valueLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Set the value at the given key in the contract storage.
    ///
    /// This version is to be used with a fixed sized storage key. For runtimes supporting
    /// transparent hashing, please use the newer version of this function.
    ///
    /// The value length must not exceed the maximum defined by the contracts module parameters.
    /// Specifying a `valueLen` of zero will store an empty value.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the location to store the value is placed.</param>
    /// <param name="valuePtr">pointer into the linear memory where the value to set is placed.</param>
    /// <param name="valueLen">the length of the value in bytes.</param>
    /// <returns>
    /// Returns the size of the pre-existing value at the specified key if any. Otherwise
    /// `SENTINEL` is returned as a sentinel value.
    /// </returns>
    private int SetStorageV1(int keyPtr, int valuePtr, int valueLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Set the value at the given key in the contract storage.
    ///
    /// The key and value lengths must not exceed the maximums defined by the contracts module
    /// parameters. Specifying a `valueLen` of zero will store an empty value.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the location to store the value is placed.</param>
    /// <param name="keyLen">the length of the key in bytes.</param>
    /// <param name="valuePtr">pointer into the linear memory where the value to set is placed.</param>
    /// <param name="valueLen">the length of the value in bytes.</param>
    /// <returns>
    /// Returns the size of the pre-existing value at the specified key if any. Otherwise
    /// `SENTINEL` is returned as a sentinel value.
    /// </returns>
    private int SetStorageV2(int keyPtr, int keyLen, int valuePtr, int valueLen)
    {
        Console.WriteLine($"SetStorage: {keyPtr}, {keyLen}, {valuePtr}, {valueLen}");

        return 0;
        /*
        fn set_storage(
			&mut self,
			key: &Key<Self::T>,
			value: Option<Vec<u8>>,
			take_old: bool,
		) -> Result<WriteOutcome, DispatchError> {
			let key = key.to_vec();
			let entry = self.storage.entry(key.clone());
			let result = match (entry, take_old) {
				(Entry::Vacant(_), _) => WriteOutcome::New,
				(Entry::Occupied(entry), false) =>
					WriteOutcome::Overwritten(entry.remove().len() as u32),
				(Entry::Occupied(entry), true) => WriteOutcome::Taken(entry.remove()),
			};
			if let Some(value) = value {
				self.storage.insert(key, value);
			}
			Ok(result)
		}
         */
    }

    public string SetStorage(byte[] key, byte[] value, bool takeOld)
    {
        _context.SetStorage(key, value, takeOld);
        return string.Empty;
    }

    /// <summary>
    /// Clear the value at the given key in the contract storage.
    ///
    /// Equivalent to the newer [`seal1`][`super::api_doc::Version1::clear_storage`] version with
    /// the exception of the return type. Still a valid thing to call when not interested in the
    /// return value.
    /// </summary>
    /// <param name="keyPtr"></param>
    private void ClearStorageV0(int keyPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Clear the value at the given key in the contract storage.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the key is placed.</param>
    /// <param name="keyLen">the length of the key in bytes.</param>
    /// <returns></returns>
    private int ClearStorageV1(int keyPtr, int keyLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieve the value under the given key from storage.
    ///
    /// This version is to be used with a fixed sized storage key. For runtimes supporting
    /// transparent hashing, please use the newer version of this function.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the key of the requested value is placed.</param>
    /// <param name="outPtr">pointer to the linear memory where the value is written to.</param>
    /// <param name="outLenPtr">
    /// in-out pointer into linear memory where the buffer length is read from and
    /// the value length is written to.
    /// </param>
    /// <returns>ReturnCode</returns>
    private int GetStorageV0(int keyPtr, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieve the value under the given key from storage.
    ///
    /// This version is to be used with a fixed sized storage key. For runtimes supporting
    /// transparent hashing, please use the newer version of this function.
    ///
    /// The key length must not exceed the maximum defined by the contracts module parameter.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the key of the requested value is placed.</param>
    /// <param name="keyLen">the length of the key in bytes.</param>
    /// <param name="outPtr">pointer to the linear memory where the value is written to.</param>
    /// <param name="outLenPtr">
    /// in-out pointer into linear memory where the buffer length is read from and
    /// the value length is written to.
    /// </param>
    /// <returns>ReturnCode</returns>
    private int GetStorageV1(int keyPtr, int keyLen, int outPtr, int outLenPtr)
    {
        Console.WriteLine($"{keyPtr}, {keyLen}, {outPtr}, {outLenPtr}");
        var key = ReadBytes(keyPtr, keyLen);
        if (_context.TryGetStorage(key, out var outcome))
        {
            Console.WriteLine($"GetStorage Success: \nKey: {key} \nValue: {outcome}");
            WriteBytes(outPtr, outcome);
            WriteUInt32(outLenPtr, Convert.ToUInt32(outcome?.Length));
            return (int)ReturnCode.Success;
        }

        return (int)ReturnCode.KeyNotFound;
    }

    /// <summary>
    /// Checks whether there is a value stored under the given key.
    ///
    /// This version is to be used with a fixed sized storage key. For runtimes supporting
    /// transparent hashing, please use the newer version of this function.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the key of the requested value is placed.</param>
    /// <returns>
    /// Returns the size of the pre-existing value at the specified key if any. Otherwise
    /// `SENTINEL` is returned as a sentinel value.
    /// </returns>
    private int ContainsStorageV0(int keyPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks whether there is a value stored under the given key.
    ///
    /// The key length must not exceed the maximum defined by the contracts module parameter.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the key of the requested value is placed.</param>
    /// <param name="keyLen">the length of the key in bytes.</param>
    /// <returns>
    /// Returns the size of the pre-existing value at the specified key if any. Otherwise
    /// `SENTINEL` is returned as a sentinel value.
    /// </returns>
    private int ContainsStorageV1(int keyPtr, int keyLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieve and remove the value under the given key from storage.
    /// </summary>
    /// <param name="keyPtr">pointer into the linear memory where the key of the requested value is placed.</param>
    /// <param name="keyLen">the length of the key in bytes.</param>
    /// <param name="outPtr">pointer to the linear memory where the value is written to.</param>
    /// <param name="outLenPtr">
    /// in-out pointer into linear memory where the buffer length is read from and
    /// the value length is written to.
    /// </param>
    /// <returns>ReturnCode</returns>
    private int TakeStorageV0(int keyPtr, int keyLen, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Transfer some value to another account.
    /// </summary>
    /// <param name="accountPtr">
    /// a pointer to the address of the beneficiary account Should be decodable as
    /// an "T:AccountId". Traps otherwise.
    /// </param>
    /// <param name="accountLen">length of the address buffer.</param>
    /// <param name="valuePtr">
    /// a pointer to the buffer with value, how much value to send. Should be
    /// decodable as a `T::Balance`. Traps otherwise.
    /// </param>
    /// <param name="valueLen">length of the value buffer.</param>
    /// <returns>ReturnCode</returns>
    private int TransferV0(int accountPtr, int accountLen, int valuePtr, int valueLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Make a call to another contract.
    /// <br/>
    /// New version available
    /// <br/>
    /// This is equivalent to calling the newer version of this function with
    /// `flags` set to `ALLOW_REENTRY`. See the newer version for documentation.
    /// <br/>
    /// Note
    /// <br/>
    /// The values `_callee_len` and `_value_len` are ignored because the encoded sizes
    /// of those types are fixed through
    /// [`codec::MaxEncodedLen`]. The fields exist
    /// for backwards compatibility. Consider switching to the newest version of this function.
    /// </summary>
    /// <returns>ReturnCode</returns>
    private int CallV0(int calleePtr, int calleeLen, long gas, int valuePtr, int valueLen, int inputDataPtr,
        int inputDataLen, int outputPtr, int outputLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Make a call to another contract.
    ///
    /// Equivalent to the newer [`seal2`][`super::api_doc::Version2::call`] version but works with
    /// <b>ref_time</b> Weight only. It is recommended to switch to the latest version, once it's
    /// stabilized.
    /// </summary>
    /// <returns>ReturnCode</returns>
    private int CallV1(int flags, int calleePtr, long gas, int valuePtr, int valueLen, int inputDataPtr,
        int inputDataLen, int outputPtr, int outputLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Make a call to another contract.
    ///
    /// The callees output buffer is copied to `output_ptr` and its length to `output_len_ptr`.
    /// The copy of the output buffer can be skipped by supplying the sentinel value
    /// of `SENTINEL` to `output_ptr`.
    /// </summary>
    /// <param name="flags">See "T:SolidityWasmRuntime.CallFlags" for a documentation of the supported flags.</param>
    /// <param name="calleePtr">
    /// a pointer to the address of the callee contract. Should be decodable as an
    /// `T::AccountId`. Traps otherwise.
    /// </param>
    /// <param name="refTimeLimit">how much <b>ref_time</b> Weight to devote to the execution.</param>
    /// <param name="proofSizeLimit">how much <b>proof_size</b> Weight to devote to the execution.</param>
    /// <param name="depositPtr">
    /// a pointer to the buffer with value of the storage deposit limit for the
    /// call. Should be decodable as a `T::Balance`. Traps otherwise. Passing `SENTINEL` means
    /// setting no specific limit for the call, which implies storage usage up to the limit of the
    /// parent call.
    /// </param>
    /// <param name="valuePtr">
    /// a pointer to the buffer with value, how much value to send. Should be
    /// decodable as a `T::Balance`. Traps otherwise.
    /// </param>
    /// <param name="inputDataPtr">a pointer to a buffer to be used as input data to the callee.</param>
    /// <param name="inputDataLen">length of the input data buffer.</param>
    /// <param name="outputPtr">a pointer where the output buffer is copied to.</param>
    /// <param name="outputLenPtr">
    /// in-out pointer to where the length of the buffer is read from and the
    /// actual length is written to.
    /// </param>
    /// <returns>ReturnCode</returns>
    private int CallV2(int flags, int calleePtr, long refTimeLimit, long proofSizeLimit, int depositPtr, int valuePtr,
        int inputDataPtr, int inputDataLen, int outputPtr, int outputLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Execute code in the context (storage, caller, value) of the current contract.
    ///
    /// Reentrancy protection is always disabled since the callee is allowed
    /// to modify the callers storage. This makes going through a reentrancy attack
    /// unnecessary for the callee when it wants to exploit the caller.
    /// </summary>
    /// <param name="flags">See "T:SolidityWasmRuntime.CallFlags" for a documentation of the supported flags.</param>
    /// <param name="codeHashPtr">a pointer to the hash of the code to be called.</param>
    /// <param name="inputDataPtr">a pointer to a buffer to be used as input data to the callee.</param>
    /// <param name="inputDataLen">length of the input data buffer.</param>
    /// <param name="outputPtr">a pointer where the output buffer is copied to.</param>
    /// <param name="outputLenPtr">
    /// in-out pointer to where the length of the buffer is read from and the
    /// actual length is written to.
    /// </param>
    /// <returns></returns>
    private int DelegateCallV0(int flags, int codeHashPtr, int inputDataPtr, int inputDataLen, int outputPtr,
        int outputLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Instantiate a contract with the specified code hash.
    /// <br/>
    /// <b>New version available</b>
    /// <br/>
    /// This is equivalent to calling the newer version of this function. The newer version
    /// drops the now unnecessary length fields.
    /// <br/>
    /// <b>Note</b>
    /// <br/>
    /// The values `_code_hash_len` and `_value_len` are ignored because the encoded sizes
    /// of those types are fixed through [`codec::MaxEncodedLen`]. The fields exist
    /// for backwards compatibility. Consider switching to the newest version of this function.
    /// </summary>
    /// <returns>ReturnCode</returns>
    private int InstantiateV0(int codeHashPtr, int codeHashLen, long gas, int valuePtr, int valueLen, int inputDataPtr,
        int inputDataLen, int addressPtr, int addressLenPtr, int outputPtr, int outputLenPtr, int saltPtr,
        int saltLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Instantiate a contract with the specified code hash.
    ///
    /// Equivalent to the newer [`seal2`][`super::api_doc::Version2::instantiate`] version but works
    /// with *ref_time* Weight only. It is recommended to switch to the latest version, once it's
    /// stabilized.
    /// </summary>
    /// <returns>ReturnCode</returns>
    private int InstantiateV1(int codeHashPtr, long gas, int valuePtr, int inputDataPtr, int inputDataLen,
        int addressPtr, int addressLenPtr, int outputPtr, int outputLenPtr, int saltPtr, int saltLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Instantiate a contract with the specified code hash.
    /// <br/>
    /// This function creates an account and executes the constructor defined in the code specified
    /// by the code hash. The address of this new account is copied to `address_ptr` and its length
    /// to `address_len_ptr`. The constructors output buffer is copied to `output_ptr` and its
    /// length to `output_len_ptr`. The copy of the output buffer and address can be skipped by
    /// supplying the sentinel value of `SENTINEL` to `output_ptr` or `address_ptr`.
    /// </summary>
    /// <param name="codeHashPtr">a pointer to the buffer that contains the initializer code.</param>
    /// <param name="refTimeLimit">how much <b>ref_time</b> Weight to devote to the execution.</param>
    /// <param name="proofSizeLimit">how much <b>proof_size</b> Weight to devote to the execution.</param>
    /// <param name="depositPtr">
    /// a pointer to the buffer with value of the storage deposit limit for the
    /// call. Should be decodable as a `T::Balance`. Traps otherwise. Passing `SENTINEL` means
    /// setting no specific limit for the call, which implies storage usage up to the limit of the
    /// parent call.
    /// </param>
    /// <param name="valuePtr">
    /// a pointer to the buffer with value, how much value to send. Should be
    /// decodable as a `T::Balance`. Traps otherwise.
    /// </param>
    /// <param name="inputDataPtr">a pointer to a buffer to be used as input data to the initializer code.</param>
    /// <param name="inputDataLen">length of the input data buffer.</param>
    /// <param name="addressPtr">a pointer where the new account's address is copied to. `SENTINEL` means not to copy.</param>
    /// <param name="addressLenPtr">pointer to where put the length of the address.</param>
    /// <param name="outputPtr">a pointer where the output buffer is copied to. `SENTINEL` means not to copy.</param>
    /// <param name="outputLenPtr">in-out pointer to where the length of the buffer is read from and the actual length is written to.</param>
    /// <param name="saltPtr">Pointer to raw bytes used for address derivation. See `fn contract_address`.</param>
    /// <param name="saltLen">length in bytes of the supplied salt.</param>
    /// <returns></returns>
    private int InstantiateV2(int codeHashPtr, long refTimeLimit, long proofSizeLimit, int depositPtr, int valuePtr,
        int inputDataPtr, int inputDataLen, int addressPtr, int addressLenPtr, int outputPtr, int outputLenPtr,
        int saltPtr, int saltLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Remove the calling account and transfer remaining balance.
    /// <br/>
    /// <b>New version available</b>
    /// <br/>
    /// This is equivalent to calling the newer version of this function. The newer version
    /// drops the now unnecessary length fields.
    /// <br/>
    /// <b>Note</b>
    /// <br/>
    /// The value `_beneficiary_len` is ignored because the encoded sizes
    /// this type is fixed through `[`MaxEncodedLen`]. The field exist for backwards
    /// compatibility. Consider switching to the newest version of this function.
    /// </summary>
    /// <param name="beneficiaryPtr"></param>
    /// <param name="beneficiaryLen"></param>
    private void TerminateV0(int beneficiaryPtr, int beneficiaryLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Remove the calling account and transfer remaining **free** balance.
    /// <br/>
    /// This function never returns. Either the termination was successful and the
    /// execution of the destroyed contract is halted. Or it failed during the termination
    /// which is considered fatal and results in a trap + rollback.
    /// <br/>
    /// <b>Traps</b>
    /// <br/>
    /// - The contract is live i.e is already on the call stack.
    /// <br/>
    /// - Failed to send the balance to the beneficiary.
    /// <br/>
    /// - The deletion queue is full.
    ///
    /// </summary>
    /// 
    /// <param name="beneficiaryPtr">
    /// a pointer to the address of the beneficiary account where all where all
    /// remaining funds of the caller are transferred. Should be decodable as an `T::AccountId`.
    /// Traps otherwise.
    /// </param>
    private void TerminateV1(int beneficiaryPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the input passed by the caller into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    /// <br/>
    /// <b>Note</b>
    /// <br/>
    /// This function traps if the input was previously forwarded by a [`call()`][`Self::call()`].
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void SealInputV0(int outPtr, int outLenPtr)
    {
        WriteBytes(outPtr, Input);
        WriteUInt32(outLenPtr, Convert.ToUInt32(Input.Length));
    }

    /// <summary>
    /// Cease contract execution and save a data buffer as a result of the execution.
    ///
    /// This function never returns as it stops execution of the caller.
    /// This is the only way to return a data buffer to the caller. Returning from
    /// execution without calling this function is equivalent to calling:
    /// <code>
    /// nocompile
    /// seal_return(0, 0, 0);
    /// </code>>
    ///
    /// The flags argument is a bitfield that can be used to signal special return
    /// conditions to the supervisor:
    /// --- lsb ---
    /// bit 0      : REVERT - Revert all storage changes made by the caller.
    /// bit [1, 31]: Reserved for future use.
    /// --- msb ---
    ///
    /// Using a reserved bit triggers a trap.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="dataPtr"></param>
    /// <param name="dataLen"></param>
    private void SealReturnV0(int flags, int dataPtr, int dataLen)
    {
        Console.WriteLine($"SealReturn: {flags}, {dataPtr}, {dataLen}");
        ReturnBuffer = new byte[dataLen];
        for (var offset = dataLen - 1; offset >= 0; offset--)
        {
            ReturnBuffer[offset] = _memory.ReadByte(dataPtr + offset);
        }
    }

    /// <summary>
    /// Stores the address of the caller into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    ///
    /// If this is a top-level call (i.e. initiated by an extrinsic) the origin address of the
    /// extrinsic will be returned. Otherwise, if this call is initiated by another contract then
    /// the address of the contract will be returned. The value is encoded as T::AccountId.
    ///
    /// If there is no address associated with the caller (e.g. because the caller is root) then
    /// it traps with `BadOrigin`.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void Caller(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks whether a specified address belongs to a contract.
    ///
    /// Returned value is a `u32`-encoded boolean: (0 = false, 1 = true).
    /// </summary>
    /// <param name="accountPtr">
    /// a pointer to the address of the beneficiary account Should be decodable as
    /// an `T::AccountId`. Traps otherwise.
    /// </param>
    /// <returns></returns>
    private int IsContract(int accountPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieve the code hash for a specified contract address.
    ///
    /// # Errors
    /// - `ReturnCode::KeyNotFound`
    /// </summary>
    /// <param name="accountPtr">
    /// a pointer to the address in question. Should be decodable as an
    /// `T::AccountId`. Traps otherwise.
    /// </param>
    /// <param name="outPtr">pointer to the linear memory where the returning value is written to.</param>
    /// <param name="outLenPtr">
    /// in-out pointer into linear memory where the buffer length is read from and
    /// the value length is written to.
    /// </param>
    /// <returns>ReturnCode</returns>
    private int CodeHash(int accountPtr, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieve the code hash of the currently executing contract.
    /// </summary>
    /// <param name="outPtr">pointer to the linear memory where the returning value is written to.</param>
    /// <param name="outLenPtr">
    /// in-out pointer into linear memory where the buffer length is read from and
    /// the value length is written to.
    /// </param>
    /// <returns></returns>
    private int OwnCodeHash(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks whether the caller of the current contract is the origin of the whole call stack.
    ///
    /// Prefer this over [`is_contract()`][`Self::is_contract`] when checking whether your contract
    /// is being called by a contract or a plain account. The reason is that it performs better
    /// since it does not need to do any storage lookups.
    ///
    /// A return value of `true` indicates that this contract is being called by a plain account
    /// and `false` indicates that the caller is another contract.
    /// 
    /// </summary>
    /// <returns>Returned value is a `u32`-encoded boolean: (`0 = false`, `1 = true`).</returns>
    private int CallerIsOrigin()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks whether the caller of the current contract is root.
    ///
    /// Note that only the origin of the call stack can be root. Hence this function returning
    /// `true` implies that the contract is being called by the origin.
    ///
    /// A return value of `true` indicates that this contract is being called by a root origin,
    /// and `false` indicates that the caller is a signed origin.
    /// 
    /// </summary>
    /// <returns>Returned value is a `u32`-encoded boolean: (`0 = false`, `1 = true`).</returns>
    private int CallerIsRoot()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the address of the current contract into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void Address(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the price for the specified amount of gas into the supplied buffer.
    ///
    /// Equivalent to the newer [`seal1`][`super::api_doc::Version2::weight_to_fee`] version but
    /// works with *ref_time* Weight only. It is recommended to switch to the latest version, once
    /// it's stabilized.
    /// </summary>
    /// <param name="gas"></param>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void WeightToFeeV0(long gas, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the price for the specified amount of weight into the supplied buffer.
    ///
    /// # Parameters
    ///
    /// - `out_ptr`: pointer to the linear memory where the returning value is written to. If the
    ///   available space at `out_ptr` is less than the size of the value a trap is triggered.
    /// - `out_len_ptr`: in-out pointer into linear memory where the buffer length is read from and
    ///   the value length is written to.
    ///
    /// The data is encoded as `T::Balance`.
    ///
    /// # Note
    ///
    /// It is recommended to avoid specifying very small values for `ref_time_limit` and
    /// `proof_size_limit` as the prices for a single gas can be smaller than the basic balance
    /// unit.
    /// </summary>
    /// <param name="refTimeLimit"></param>
    /// <param name="proofSizeLimit"></param>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void WeightToFeeV1(long refTimeLimit, long proofSizeLimit, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the weight left into the supplied buffer.
    ///
    /// Equivalent to the newer [`seal1`][`super::api_doc::Version2::gas_left`] version but
    /// works with *ref_time* Weight only. It is recommended to switch to the latest version, once
    /// it's stabilized.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void GasLeftV0(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the amount of weight left into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    ///
    /// The data is encoded as Weight.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void GasLeftV1(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the *free* balance of the current account into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    ///
    /// The data is encoded as `T::Balance`.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void Balance(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the value transferred along with this call/instantiate into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a `u32` value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    ///
    /// The data is encoded as `T::Balance`.
    /// </summary>
    /// <param name="valuePtr"></param>
    /// <param name="valueLengthPtr"></param>
    private void ValueTransferred(int valuePtr, int valueLengthPtr)
    {
        WriteUInt32(valueLengthPtr, 0);
    }

    /// <summary>
    /// Stores a random number for the current block and the given subject into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    ///
    /// The data is encoded as `T::Hash`.
    /// </summary>
    /// <param name="subjectPtr"></param>
    /// <param name="subjectLenPtr"></param>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    /// <returns></returns>
    private int RandomV0(int subjectPtr, int subjectLenPtr, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores a random number for the current block and the given subject into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    ///
    /// The data is encoded as (T::Hash, frame_system::pallet_prelude::BlockNumberFor::<T>).
    ///
    /// # Changes from v0
    ///
    /// In addition to the seed it returns the block number since which it was determinable
    /// by chain observers.
    ///
    /// # Note
    ///
    /// The returned seed should only be used to distinguish commitments made before
    /// the returned block number. If the block number is too early (i.e. commitments were
    /// made afterwards), then ensure no further commitments may be made and repeatedly
    /// call this on later blocks until the block number returned is later than the latest
    /// commitment.
    /// </summary>
    /// <param name="subjectPtr"></param>
    /// <param name="subjectLenPtr"></param>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    /// <returns></returns>
    private int RandomV1(int subjectPtr, int subjectLenPtr, int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Load the latest block timestamp into the supplied buffer
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void Now(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the minimum balance (a.k.a. existential deposit) into the supplied buffer.
    ///
    /// The data is encoded as `T::Balance`.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void MinimumBalance(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Deposit a contract event with the data buffer and optional list of topics. There is a limit
    /// on the maximum number of topics specified by `event_topics`.
    /// </summary>
    /// <param name="topicsPtr">
    /// a pointer to the buffer of topics encoded as `Vec T::Hash`. The value of
    /// this is ignored if `topics_len` is set to `0`. The topics list can't contain duplicates.
    /// </param>
    /// <param name="topicsLen">the length of the topics buffer. Pass 0 if you want to pass an empty vector.</param>
    /// <param name="dataPtr">a pointer to a raw data buffer which will saved along the event.</param>
    /// <param name="dataLen">the length of the data buffer.</param>
    private void DepositEvent(int topicsPtr, int topicsLen, int dataPtr, int dataLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the current block number of the current contract into the supplied buffer.
    ///
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// `out_len_ptr` must point to a u32 value that describes the available space at
    /// `out_ptr`. This call overwrites it with the size of the value. If the available
    /// space at `out_ptr` is less than the size of the value a trap is triggered.
    /// </summary>
    /// <param name="outPtr"></param>
    /// <param name="outLenPtr"></param>
    private void BlockNumber(int outPtr, int outLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Computes the SHA2 256-bit hash on the given input buffer.
    ///
    /// Returns the result directly into the given output buffer.
    ///
    /// # Note
    ///
    /// - The `input` and `output` buffer may overlap.
    /// - The output buffer is expected to hold at least 32 bytes (256 bits).
    /// - It is the callers responsibility to provide an output buffer that is large enough to hold
    ///   the expected amount of bytes returned by the chosen hash function.
    /// </summary>
    /// <param name="inputPtr">the pointer into the linear memory where the input data is placed.</param>
    /// <param name="inputLen">the length of the input data in bytes.</param>
    /// <param name="outputPtr">
    /// the pointer into the linear memory where the output data is placed. The
    /// function will write the result directly into this buffer.
    /// </param>
    private void HashSha2_256(int inputPtr, int inputLen, int outputPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Computes the KECCAK 256-bit hash on the given input buffer.
    ///
    /// Returns the result directly into the given output buffer.
    ///
    /// # Note
    ///
    /// - The `input` and `output` buffer may overlap.
    /// - The output buffer is expected to hold at least 32 bytes (256 bits).
    /// - It is the callers responsibility to provide an output buffer that is large enough to hold
    ///   the expected amount of bytes returned by the chosen hash function.
    /// </summary>
    /// <param name="inputPtr">the pointer into the linear memory where the input data is placed.</param>
    /// <param name="inputLen">the length of the input data in bytes.</param>
    /// <param name="outputPtr">
    /// the pointer into the linear memory where the output data is placed. The
    /// function will write the result directly into this buffer.
    /// </param>
    private void HashKeccak256(int inputPtr, int inputLen, int outputPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Computes the BLAKE2 256-bit hash on the given input buffer.
    ///
    /// Returns the result directly into the given output buffer.
    ///
    /// # Note
    ///
    /// - The `input` and `output` buffer may overlap.
    /// - The output buffer is expected to hold at least 32 bytes (256 bits).
    /// - It is the callers responsibility to provide an output buffer that is large enough to hold
    ///   the expected amount of bytes returned by the chosen hash function.
    /// </summary>
    /// <param name="inputPtr">the pointer into the linear memory where the input data is placed.</param>
    /// <param name="inputLen">the length of the input data in bytes.</param>
    /// <param name="outputPtr">
    /// the pointer into the linear memory where the output data is placed. The
    /// function will write the result directly into this buffer.
    /// </param>
    private void HashBlake2_256(int inputPtr, int inputLen, int outputPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Computes the BLAKE2 128-bit hash on the given input buffer.
    ///
    /// Returns the result directly into the given output buffer.
    ///
    /// # Note
    ///
    /// - The `input` and `output` buffer may overlap.
    /// - The output buffer is expected to hold at least 32 bytes (256 bits).
    /// - It is the callers responsibility to provide an output buffer that is large enough to hold
    ///   the expected amount of bytes returned by the chosen hash function.
    /// </summary>
    /// <param name="inputPtr">the pointer into the linear memory where the input data is placed.</param>
    /// <param name="inputLen">the length of the input data in bytes.</param>
    /// <param name="outputPtr">
    /// the pointer into the linear memory where the output data is placed. The
    /// function will write the result directly into this buffer.
    /// </param>
    private void HashBlake2_128(int inputPtr, int inputLen, int outputPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Call into the chain extension provided by the chain if any.
    ///
    /// Handling of the input values is up to the specific chain extension and so is the
    /// return value. The extension can decide to use the inputs as primitive inputs or as
    /// in/out arguments by interpreting them as pointers. Any caller of this function
    /// must therefore coordinate with the chain that it targets.
    ///
    /// # Note
    ///
    /// If no chain extension exists the contract will trap with the `NoChainExtension`
    /// module error.
    /// </summary>
    private void CallChainExtension(int id, int inputPtr, int inputLen, int outputPtr, int outputLenPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Emit a custom debug message.
    ///
    /// No newlines are added to the supplied message.
    /// Specifying invalid UTF-8 just drops the message with no trap.
    ///
    /// This is a no-op if debug message recording is disabled which is always the case
    /// when the code is executing on-chain. The message is interpreted as UTF-8 and
    /// appended to the debug buffer which is then supplied to the calling RPC client.
    ///
    /// # Note
    ///
    /// Even though no action is taken when debug message recording is disabled there is still
    /// a non trivial overhead (and weight cost) associated with calling this function. Contract
    /// languages should remove calls to this function (either at runtime or compile time) when
    /// not being executed as an RPC. For example, they could allow users to disable logging
    /// through compile time flags (cargo features) for on-chain deployment. Additionally, the
    /// return value of this function can be cached in order to prevent further calls at runtime.
    /// </summary>
    /// <param name="strPtr"></param>
    /// <param name="strLen"></param>
    /// <returns></returns>
    private int SealDebugMessage(int strPtr, int strLen)
    {
        DebugMessages.Add(_memory.ReadString(strPtr, strLen));
        return 0;
    }

    /// <summary>
    /// Call some dispatchable of the runtime.
    ///
    /// This function decodes the passed in data as the overarching `Call` type of the
    /// runtime and dispatches it. The weight as specified in the runtime is charged
    /// from the gas meter. Any weight refunds made by the dispatchable are considered.
    ///
    /// The filter specified by `Config::CallFilter` is attached to the origin of
    /// the dispatched call.
    ///
    /// # Comparison with `ChainExtension`
    ///
    /// Just as a chain extension this API allows the runtime to extend the functionality
    /// of contracts. While making use of this function is generally easier it cannot be
    /// used in all cases. Consider writing a chain extension if you need to do perform
    /// one of the following tasks:
    ///
    /// - Return data.
    /// - Provide functionality **exclusively** to contracts.
    /// - Provide custom weights.
    /// - Avoid the need to keep the `Call` data structure stable.
    /// </summary>
    /// <param name="callPtr">the pointer into the linear memory where the input data is placed.</param>
    /// <param name="callLen">the length of the input data in bytes.</param>
    /// <returns>
    /// Returns `ReturnCode::Success` when the dispatchable was successfully executed and
    /// returned `Ok`. When the dispatchable was exeuted but returned an error
    /// `ReturnCode::CallRuntimeFailed` is returned. The full error is not
    /// provided because it is not guaranteed to be stable.
    /// </returns>
    private int CallRuntime(int callPtr, int callLen)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Recovers the ECDSA public key from the given message hash and signature.
    ///
    /// Writes the public key into the given output buffer.
    /// Assumes the secp256k1 curve.
    /// </summary>
    /// <param name="signaturePtr">
    /// the pointer into the linear memory where the signature is placed. Should
    /// be decodable as a 65 bytes. Traps otherwise.
    /// </param>
    /// <param name="messageHashPtr">
    /// the pointer into the linear memory where the message hash is placed.
    /// Should be decodable as a 32 bytes. Traps otherwise.
    /// </param>
    /// <param name="outputPtr">
    /// the pointer into the linear memory where the output data is placed. The
    /// buffer should be 33 bytes. The function will write the result directly into this buffer.
    /// </param>
    /// <returns>ReturnCode</returns>
    private int EcdsaRecover(int signaturePtr, int messageHashPtr, int outputPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Verify a sr25519 signature
    /// </summary>
    /// <param name="signaturePtr">
    /// the pointer into the linear memory where the signature is placed. Should
    /// be a value of 64 bytes.
    /// </param>
    /// <param name="pubKeyPtr">
    /// the pointer into the linear memory where the public key is placed. Should
    /// be a value of 32 bytes.
    /// </param>
    /// <param name="messageLen">the length of the message payload.</param>
    /// <param name="messagePtr">the pointer into the linear memory where the message is placed.</param>
    /// <returns>ReturnCode</returns>
    private int Sr25519Verify(int signaturePtr, int pubKeyPtr, int messageLen, int messagePtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Replace the contract code at the specified address with new code.
    ///
    /// # Note
    ///
    /// There are a couple of important considerations which must be taken into account when
    /// using this API:
    ///
    /// 1. The storage at the code address will remain untouched. This means that contract
    /// developers must ensure that the storage layout of the new code is compatible with that of
    /// the old code.
    ///
    /// 2. Contracts using this API can't be assumed as having deterministic addresses. Said another
    /// way, when using this API you lose the guarantee that an address always identifies a specific
    /// code hash.
    ///
    /// 3. If a contract calls into itself after changing its code the new call would use
    /// the new code. However, if the original caller panics after returning from the sub call it
    /// would revert the changes made by [`set_code_hash()`][`Self::set_code_hash`] and the next
    /// caller would use the old code.
    /// </summary>
    /// <param name="codeHashPtr">A pointer to the buffer that contains the new code hash.</param>
    /// <returns>ReturnCode</returns>
    private int SetCodeHash(int codeHashPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Calculates Ethereum address from the ECDSA compressed public key and stores
    /// it into the supplied buffer.
    /// <br/>
    /// The value is stored to linear memory at the address pointed to by `out_ptr`.
    /// If the available space at `out_ptr` is less than the size of the value a trap is triggered.
    /// </summary>
    /// <param name="keyPtr">
    /// a pointer to the ECDSA compressed public key. Should be decodable as a 33 bytes
    /// value. Traps otherwise.
    /// </param>
    /// <param name="outPtr">
    /// the pointer into the linear memory where the output data is placed. The
    /// function will write the result directly into this buffer.
    /// </param>
    /// <returns></returns>
    private int EcdsaToEthAddress(int keyPtr, int outPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the number of times the currently executing contract exists on the call stack in
    /// addition to the calling instance.
    /// </summary>
    /// <returns>Returns `0` when there is no reentrancy.</returns>
    private int ReentranceCount()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the number of times specified contract exists on the call stack. Delegated calls are
    /// not counted as separate calls.
    /// </summary>
    /// <param name="accountPtr">a pointer to the contract address.</param>
    /// <returns>Returns `0` when the contract does not exist on the call stack.</returns>
    private int AccountReentranceCount(int accountPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a nonce that is unique per contract instantiation.
    ///
    /// The nonce is incremented for each successful contract instantiation. This is a
    /// sensible default salt for contract instantiations.
    /// </summary>
    /// <returns></returns>
    private int InstantiationNonce()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds a new delegate dependency to the contract.
    /// </summary>
    /// <param name="codeHashPtr">A pointer to the code hash of the dependency.</param>
    private void AddDelegateDependency(int codeHashPtr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Removes the delegate dependency from the contract.
    /// </summary>
    /// <param name="codeHashPtr">A pointer to the code hash of the dependency.</param>
    private void RemoveDelegateDependency(int codeHashPtr)
    {
        throw new NotImplementedException();
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

    private byte[] ReadBytes(int address, int length)
    {
        var value = new byte[length];
        for (var i = 0; i < length; i++)
        {
            value[i] = _memory.ReadByte(address + i);
        }

        return value;
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
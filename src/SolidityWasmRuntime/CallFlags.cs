namespace SolidityWasmRuntime;

/// <summary>
/// Flags used to change the behaviour of `seal_call` and `seal_delegate_call`.
/// </summary>
public struct CallFlags
{
    /// <summary>
    /// Forward the input of current function to the callee.
    ///
    /// Supplied input pointers are ignored when set.
    ///
    /// # Note
    ///
    /// A forwarding call will consume the current contracts input. Any attempt to
    /// access the input after this call returns will lead to [`Error::InputForwarded`].
    /// It does not matter if this is due to calling `seal_input` or trying another
    /// forwarding call. Consider using [`Self::CLONE_INPUT`] in order to preserve
    /// the input.
    /// </summary>
    public const int ForwardInput = 0b0000_0001;

    /// <summary>
    /// Identical to [`Self::FORWARD_INPUT`] but without consuming the input.
    ///
    /// This adds some additional weight costs to the call.
    ///
    /// # Note
    ///
    /// This implies [`Self::FORWARD_INPUT`] and takes precedence when both are set.
    /// </summary>
    public const int CloneInput = 0b0000_0010;

    /// <summary>
    /// Do not return from the call but rather return the result of the callee to the
    /// callers caller.
    ///
    /// # Note
    ///
    /// This makes the current contract completely transparent to its caller by replacing
    /// this contracts potential output by the callee ones. Any code after `seal_call`
    /// can be safely considered unreachable.
    /// </summary>
    public const int TailCall = 0b0000_0100;

    /// <summary>
    /// Allow the callee to reenter into the current contract.
    ///
    /// Without this flag any reentrancy into the current contract that originates from
    /// the callee (or any of its callees) is denied. This includes the first callee:
    /// You cannot call into yourself with this flag set.
    ///
    /// # Note
    ///
    /// For `seal_delegate_call` should be always unset, otherwise
    /// [`Error::InvalidCallFlags`] is returned.
    /// </summary>
    public const int AllowReentry = 0b0000_1000;
}
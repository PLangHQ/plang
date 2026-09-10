# BlockchainModule

Hint: `[blockchain]`  
Type: `PLang.Modules.BlockchainModule.Program`

Use blockchain, create wallet, account info, transfer money

## Methods

### AllowanceFromSmartContract

```
AllowanceFromSmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger value) : Object
```


### ApproveSmartContract

```
ApproveSmartContract(String contractAddressOrSymbol, String spender, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object
```


### BalanceOfBatchOnSmartContract

```
BalanceOfBatchOnSmartContract(String contractAddressOrSymbol, String[] addresses, Numerics.BigInteger[] ids) : Object
```


### BalanceOfOnSmartContract

```
BalanceOfOnSmartContract(String contractAddressOrSymbol, String addressToCheckBalanceOf) : Object
```


### BurnSmartContract

```
BurnSmartContract(String contractAddressOrSymbol, String account, Numerics.BigInteger amount, Boolean waitForReceipt = False) : Object
```


### CallAndSignFunction

```
CallAndSignFunction(String contractAddressOrSymbol, String abi, Object[] functionInputs = null, Boolean waitForReceipt = False) : Object
```

Generate abi from functionName and functionInputs if not provided by user.


### CallFunction

```
CallFunction(String contractAddressOrSymbol, String abi, Object[] functionInputs = null) : Object
```


### CecimalsOnSmartContract

```
CecimalsOnSmartContract(String contractAddressOrSymbol) : Object
```


### GetApprovedOnSmartContract

```
GetApprovedOnSmartContract(String contractAddressOrSymbol) : Object
```


### GetCurrentAddress

```
GetCurrentAddress() : String
```


### GetCurrentRpcServer

```
GetCurrentRpcServer() : PLang.Modules.BlockchainModule.ModuleSettings+RpcServer
```


### GetDecimal

```
GetDecimal(String contractAddress) : UInt32
```


### GetMyBalanceOnSmartContract

```
GetMyBalanceOnSmartContract(String contractAddressOrSymbol) : Object
```


### GetNativeBalanceOfAddressInWei

```
GetNativeBalanceOfAddressInWei(String address) : Numerics.BigInteger
```


### GetNativeBalanceOfAddressToDecimalPoint

```
GetNativeBalanceOfAddressToDecimalPoint(String address, Int32 decimalPlacesToUnit = 18) : Decimal
```

Get the balance in ETH, converts from Wei to Eth


### GetOrCreateWallet

```
GetOrCreateWallet() : Nethereum.HdWallet.Wallet
```


### GetPrivateKey

```
GetPrivateKey() : String
```


### GetRpcServers

```
GetRpcServers() : PLang.Modules.BlockchainModule.ModuleSettings+RpcServer
```


### GetUriOnSmartContract

```
GetUriOnSmartContract(String contractAddressOrSymbol, Numerics.BigInteger id) : Object
```


### GetWallets

```
GetWallets() : PLang.Modules.BlockchainModule.ModuleSettings+Wallet
```


### IsApprovedForAllOnSmartContract

```
IsApprovedForAllOnSmartContract(String contractAddressOrSymbol, String accountAddress, String operatorAddress) : Object
```


### ListenToApprovalEventOnSmartContract

```
ListenToApprovalEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### ListenToApprovalForAllEventOnSmartContract

```
ListenToApprovalForAllEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### ListenToBlock

```
ListenToBlock(PLang.Models.GoalToCallInfo callGoal, String subcriptionId = subscriptionId, PLang.Models.GoalToCallInfo callGoalOnUnsubscribe = null) : object
```

- `callGoal` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subcriptionId` *String*, default `subscriptionId`
- `callGoalOnUnsubscribe` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### ListenToEventOnSmartContract

```
ListenToEventOnSmartContract(String contractAddressOrSymbol, String abi, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `abi` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### ListenToTransferBatchEventOnSmartContract

```
ListenToTransferBatchEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### ListenToTransferEventOnSmartContract

```
ListenToTransferEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### ListenToTransferSingleEventOnSmartContract

```
ListenToTransferSingleEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### ListenToUriEventOnSmartContract

```
ListenToUriEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object
```

- `contractAddressOrSymbol` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `subscriptIdVariableName` *String*, default `subscriptionId`

### MintSmartContract

```
MintSmartContract(String contractAddressOrSymbol, String to, Numerics.BigInteger amount, Boolean waitForReceipt = False) : Object
```


### NameOfSmartContract

```
NameOfSmartContract(String contractAddressOrSymbol) : Object
```


### SafeBatchTransferFromSmartContract

```
SafeBatchTransferFromSmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object
```


### SafeTransferFromErc1155SmartContract

```
SafeTransferFromErc1155SmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger[] ids, Numerics.BigInteger[] amounts, Boolean waitForReceipt = False) : Object
```


### SafeTransferFromErc721SmartContract

```
SafeTransferFromErc721SmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger id, Boolean waitForReceipt = False) : Object
```


### SendTransaction

```
SendTransaction(String contractAddress, String abi, Object[] args) : String
```

Send a transaction and returns transaction hash


### SendTransactionAndWaitForReceipt

```
SendTransactionAndWaitForReceipt(String contractAddress, String abi, Object[] args) : Nethereum.RPC.Eth.DTOs.TransactionReceipt
```

Send a transaction and waits for the transaction to finish


### SetApprovalForAllOnSmartContract

```
SetApprovalForAllOnSmartContract(String contractAddressOrSymbol, String operatorAddress, Boolean approved, Boolean waitForReceipt = False) : Object
```


### SetCurrentAddress

```
SetCurrentAddress(String address) : object
```


### SetCurrentRpcServer

```
SetCurrentRpcServer(String nameOrUrl) : object
```


### SetCurrentWallet

```
SetCurrentWallet(String walletName) : object
```


### SignTransfer

```
SignTransfer(String recipient, String smartContractSymbolOrAddress, Int64 value) : String
```


### StopListening

```
StopListening(String subscriptionId) : object
```


### SupportsInterfaceOnSmartContract

```
SupportsInterfaceOnSmartContract(String contractAddressOrSymbol, String interfaceId) : Object
```


### SymbolOnSmartContract

```
SymbolOnSmartContract(String contractAddressOrSymbol) : Object
```


### TotalSupplyOnSmartContract

```
TotalSupplyOnSmartContract(String contractAddressOrSymbol) : Object
```


### Transfer

```
Transfer(String to, Decimal etherAmount, Nullable<Decimal> gasPriceWei = null, Nullable<Numerics.BigInteger> gas = null, Nullable<Numerics.BigInteger> nonce = null) : String
```


### TransferFromSmartContract

```
TransferFromSmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object
```


### TransferSmartContract

```
TransferSmartContract(String contractAddressOrSymbol, String to, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object
```


### TransferWaitForReceipt

```
TransferWaitForReceipt(String to, Decimal etherAmount, Nullable<Decimal> gasPriceWei = null, Nullable<Numerics.BigInteger> gas = null, Nullable<Numerics.BigInteger> nonce = null) : Nethereum.RPC.Eth.DTOs.TransactionReceipt
```



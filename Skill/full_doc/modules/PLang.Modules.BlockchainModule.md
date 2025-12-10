
# Blockchain

## Introduction
Blockchain technology is a decentralized digital ledger that records transactions across many computers in such a way that the registered transactions cannot be altered retroactively. This technology is the backbone of cryptocurrencies and has various applications in different sectors due to its security and transparency features.

## For beginners
Blockchain is like a digital notebook that everyone can see, but no one can erase or change what's been written. Imagine a book where you can write down who gave money to whom, and once it's written, it's permanent and visible to everyone. This makes it very trustworthy because once something is recorded, it can't be tampered with. It's not just for money; it can be used to keep track of all sorts of agreements and exchanges.

## Best Practices for Blockchain
When writing plang code for blockchain applications, it's crucial to follow best practices to ensure security, efficiency, and reliability. Here's an example to illustrate:

```plang
- set to mumbai testnet
- transfer to 0x1234....., ether amount 0.1, gas price 50 gwei, write to %transactionHash%
- if %transactionHash% is not empty then call !TransactionSuccess, else !TransactionFailed

TransactionSuccess
- write out "Transaction successful with hash: %transactionHash%"

TransactionFailed
- write out "Transaction failed, please try again or check your balance."
```

In this example, we're using the Mumbai testnet to simulate a transaction, which is a best practice for development. We're also handling the outcome of the transaction with `if` statements, ensuring that the user is informed whether the transaction was successful or not.


# Blockchain Module Examples

The following examples demonstrate how to use the Blockchain Module in plang. They are sorted by their estimated popularity and usage in real-world scenarios.

## Getting Wallet and Address Information

### Get Current RPC Server
```plang
- get current rpc server, write to %server%
- write out %server.ToJson()%
```

### List All Wallets
```plang
- get wallets, write to %wallets%
- write out %wallets.ToJson()%
```

### Get Current Address
```plang
- get current address, write to %address%
- write out %address%
```

## Signing and Verifying Messages

### Sign a Message
```plang
- sign "Hello world", write to %signature%
```

### Verify a Signature
```plang
- verify signature, "Hello world", %signature%, %address%, write to %result%
- write out "Is message verified: %result%"
```

## Interacting with Smart Contracts

### Get Token Balance
```plang
- set to mumbai testnet
- get wei balance of 0x50041223216d8bfd392544562d70fda452df5042, write to %maticBalance%
- get balance on 0x0FA8781a83E46826621b3BC094Ea2A0212e71B23 for address 0x50041223216d8bfd392544562d70fda452df5042, write to %usdcBalance%
- write out 'Balance of Matic: %maticBalance%, USDC balance: %usdcBalance%'
```

### Get Token Decimal and Symbol
```plang
- get decimal of 0x0FA8781a83E46826621b3BC094Ea2A0212e71B23, write to %decimal%
- get symbol of 0x0FA8781a83E46826621b3BC094Ea2A0212e71B23, write to %symbol%
- write out '%symbol% has %decimal% decimals'
```

## Listening to Events

### Listen to Transfer Events on a Smart Contract
```plang
- set chain, matic
- listen to usdc, Transfer event, call !Transfer
    if exception is 500, call !WriteOutSetupMessage

Transfer
- write out "from: %from% | to: %to% | value: %value%"
- write out %__TxLog__.ToJson()%
- stop listening %subscriptionId%
```


### Listen to New Blocks
```plang
- listen to new block, call !BlockEvent
```

### Handle New Block Event
```plang
BlockEvent
- write out "timestamp: %timestamp% | lastBlockNotification: %lastBlockNotification%"
- write out block: %block.ToJson()%
```

## Transferring Ether

### Transfer Ether
```plang
- transfer to 0x1234...., ether amount 0.1, gas price 50 gwei, write to %transactionHash%
- write out "Transaction hash: %transactionHash%"
```

### Transfer Ether and Wait for Receipt
```plang
- transfer to 0x1234...., ether amount 0.1, gas price 50 gwei, wait for receipt, write to %transactionReceipt%
- write out "Transaction receipt: %transactionReceipt.ToJson()%"
```

## Additional Examples

### Get Current Wallet
```plang
- get or create wallet, write to %currentWallet%
- write out %currentWallet.ToJson()%
```

### Set Current Wallet
```plang
- set current wallet, "MyWalletName"
```

### Get Balance in Wei
```plang
- get balance in wei of 0x50041223216d8bfd392544562d70fda452df5042, write to %balanceInWei%
- write out "Balance in Wei: %balanceInWei%"
```

### Get Balance with Decimal Points
```plang
- get balance to decimal point of 0x50041223216d8bfd392544562d70fda452df5042, decimal places 2, write to %balance%
- write out "Balance: %balance% ETH"
```

These examples provide a starting point for interacting with the blockchain using plang. They cover a range of common tasks such as getting wallet information, signing and verifying messages, interacting with smart contracts, listening to events, and transferring ether.


For a full list of examples, visit [PLang Blockchain Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Blockchain).

## Step options
Each step in plang can be enhanced with various options for better control and error handling. Click on the links below for more details on how to use each option:

- [CacheHandler](/CachingHandler.md)
- [ErrorHandler](/ErrorHandler.md)




## Advanced
For those who are interested in diving deeper into the technical aspects of the Blockchain Module in plang, please refer to the [advanced documentation](./PLang.Modules.BlockchainModule_advanced.md). This section covers the intricate details of how plang interfaces with C# to provide blockchain functionalities.

## Created
This documentation was created on 2024-01-02T21:24:54.

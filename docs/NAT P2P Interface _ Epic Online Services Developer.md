Epic Developer [Resources](https://dev.epicgames.com/docs)

NAT P2P Interface

# NAT P2P Interface

Interface to send and receive data between users, and related networking functionality.

Integrate the P2P Interface into your game (or other software application) to use the features of the Epic Online Services (EOS) P2P service. The interface, which enables games implementing the EOS SDK to set up and manage peer-to-peer (P2P) connections between players (users), uses Network Address Translation (NAT).

P2P connections enable game clients to send and receive data between one another directly, typically for the purpose of a multiplayer game. Connections made with the EOS P2P Interface are only established between authenticated users, and are secure-by-default using Datagram Transport Layer (DTLS) (Wikipedia: [DLTS] ([https://en.wikipedia.org/wiki/Datagram\\_Transport\\_Layer\\_Security\)](https://en.wikipedia.org/wiki/Datagram_Transport_Layer_Security). DTLS provides two distinct advantages over other communications protocols: \*The speed of handling P2P connections is significantly increased, resulting in EOS's authentication having a greatly reduced need for connections to be re-negotiated.

The process of securely handling connections simplifies EOS SDK integration with your game, abstracting out the need for detailed network socket management and condensing most functions to what data needs to be sent and to whom.

## Accessing the P2P Interface

In order to use the functions within the EOS P2P Interface, you must first obtain a valid EOS\_HP2P handle from the Platform Interface function EOS\_Patform\_GetP2PInterface , because this handle is used in all P2P Interface functions. For more information about the Platform Interface and this function, see documentation: EOS Platform [Interface](https://dev.epicgames.com/docs/en-US/game-services/eos-platform-interface).

# Managing P2P Connections

The EOS SDK has timeout settings for P2P connections. We don't recommend overriding connection timeouts in your game code.

In EOS SDK 1.16.2, the ReceivePacketOptions and SocketId arguments for the ReceivePacket API are now references. Before EOS SDK 1.16.2 they were out parameters. For more information, see the [Release](https://dev.epicgames.com/docs/en-US/epic-online-services/release-notes#release-notes) Notes.

The P2P Interface uses the struct EOS\_P2P\_SocketId as a title-specified identifier for a connection between peers. Most P2P functions related to connections either require an EOS\_P2P\_SocketId to associate with a connection request, or to return one in order to specify what connection a received connection request is associated with. EOS\_P2P\_SocketId is comprised of the following parameters:

| Parameter  | Description                                                       |
|------------|-------------------------------------------------------------------|
| ApiVersion | A version field. This must be set to EOS P2P SOCKETID API LATEST. |

| Parameter  | Description                                                                                                                                                        |
|------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| SocketName | A value used to vet all incoming connections from unknown peers in multiplayer games. A<br>SocketName must contain 1-32 alpha-numeric, null-terminated characters. |

The SocketName field can be a single value for all connections, or it can be a secret value known only to specific players in a multiplayer session. As accepted P2P connections expose a user's IP address to the peer that they are connecting to, it is important to not blindly accept any connection request.

**Note**: The P2P Interface uses Simple Text Oriented Messaging Protocol (STOMP) messaging notifications, with websockets, when a player accepts a connection or when they request a connection. You might see STOMP in the EOS Messaging category of EOS SDK logs.

A valid EOS\_ProductUserId is also usually required both for users to identify themselves when sending data, and to specify which user they wish to send data to.

All function parameters and their associated values in the P2P Interface are required unless explicitly marked as optional. This includes out-parameters, which the P2P Interface often uses to output data for asynchronous functions or event responses. If a function has a return type of EOS\_EResut and the return value is not EOS\_Success , then take note that any out-parameters that the function provides will be unset unless specified otherwise.

#### Requesting Connections

When a local user attempts to send information to a remote user, such as with the EOS\_P2P\_SendPacket function, the P2P Interface will automatically make a request to open a connection between those two users, with an EOS\_P2P\_SocketId acting as the identifier for that connection. The user sending the information automatically accepts their own request for that SocketId, while the user receiving the information must accept it, usually by listening for the incoming connection request and using the EOS\_P2P\_AcceptConnection function.

All operations requiring an open P2P connection can be used to both request and accept a P2P connection if one is not already open. For example, EOS\_P2P\_AcceptConnection can be used to request a P2P connection with a remote user, and EOS\_P2P\_SendPacket can be used to accept a remote user's P2P connection request if one has already been made for the Socket Id that the local user is trying to use to send information. This effectively means that it is possible to use EOS\_P2P\_AcceptConnection to pre-emptively accept the next connection request made with a given SocketId.

If multiple connections are open with a particular user, only the first connection must be negotiated. This greatly increases the speed at which data may be received with subsequent connection requests.

#### **Common Connection Failure Causes**

Your players' peer-to-peer connection might fail if they have one or more of these issues:

- A player's internet is unavailable.
- A player's operating system is suspended or hibernating.
- A player runs into a bug in your game's code.

### Receiving Connection Request Notifications

When a user receives a connection request, a notify event is fired to all bound **Peer Connection Request Handlers**.

To listen for connection requests with a new Peer Connection Request Handler, use

EOS\_P2P\_AddNotifyPeerConnectionRequest to bind a function that you would like to use as a response to incoming connection requests. The EOS\_P2P\_AddNotifyPeerConnectionRequest function takes the following parameters:

| Parameter                | Description                                                                                                                                                                                                                |
|--------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Handle                   | A valid EOS_HP2P handle received from the Platform Interface.                                                                                                                                                              |
| Options                  | A pointer to an EOS_P2P_AddNotifyPeerConnectionRequestOptions struct.                                                                                                                                                      |
| ClientData (Optional)    | Pointer to data that will be returned to the caller when the event fires.                                                                                                                                                  |
| ConnectionRequestHandler | A function that will act as your Peer Connection Request Handler, responding to<br>incoming connection requests. This function must take a pointer to an<br>EOS_P2P_OnIncomingConnectionRequestInfo struct as a parameter. |

The EOS\_P2P\_AddNotifyPeerConnectionRequestOptions struct is comprised of the following parameters:

| Parameter              | Description                                                                                             |
|------------------------|---------------------------------------------------------------------------------------------------------|
| ApiVersion             | A version field. Set to EOS_P2P_ADDNOTIFYPEERCONNECTIONREQUEST_API_LATEST                               |
| LocalUserId            | Set to the ID of the local user that is listening for incoming connection request.                      |
| SocketId<br>(Optional) | Pointer to a valid EOS_P2P_SocketId struct that you would like to use to filter<br>connection requests. |

The EOS\_P2P\_AddNotifyPeerConnectionRequest function will return either a valid EOS\_NotificationId on success, or the value EOS\_INVALID\_NOTIFICATIONID on failure.

If the network status changes from offline to online, you must call EOS\_P2P\_AddNotifyPeerConnectionRequest again. If you do not call this function again, any incoming P2P connections will fail.

You can remove a Peer Connection Request Handler with the EOS\_P2P\_RemoveNotifyPeerConnectionRequest function, which requires the following parameters:

| Parameter      | Description                                                                                                                                                   |
|----------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Handle         | A valid EOS_HP2P handle received from the Platform Interface.                                                                                                 |
| NotificationId | A valid EOS_NotificationId returned from EOS_P2P_AddNotifyPeerConnectionRequest ,<br>used to identify the peer connection request handler you wish to remove. |

#### Accepting Connections

The EOS\_P2P\_AcceptConnection function can be used to accept a connection that has been requested, or to start a new connection request. Both users must accept a connection for a given SocketId before the connection can be established and used to send data. The user attempting to open a connection has to call EOS\_P2P\_AcceptConnection , if bDisabeAutoAcceptConnection is set to EOS\_TRUE .

To call the EOS\_P2P\_AcceptConnection function, you must provide the following parameters:

| Parameter | Description                                                   |
|-----------|---------------------------------------------------------------|
| Handle    | A valid EOS_HP2P handle received from the Platform Interface. |
| Options   | A pointer to an EOS_P2P_AcceptConnectionOptions struct.       |

The EOS\_P2P\_AcceptConnectionOptions struct contains the following parameters:

| Parameter    | Description                                                                                |
|--------------|--------------------------------------------------------------------------------------------|
| ApiVersion   | Set to EOS_P2P_ACCEPTCONNECTION_API_LATEST                                                 |
| LocalUserId  | Set to the EOS_ProductUserId of the local user that is accepting the connection.           |
| RemoteUserId | Set to the EOS_ProductUserId of the remote user that the local user wants to connect to.   |
| SocketId     | Pointer to a valid EOS_P2P_SocketId struct that you would like to accept a connection for. |

The EOS\_P2P\_AcceptConnection function will return a value of EOS\_InvaidParameters if any of the supplied input was invalid. It will return EOS\_Success if the supplied information was valid, signifying that the connection was locally accepted and will be able to send data when the remote user accepts the connection as well. Calling EOS\_P2P\_AcceptConnection multiple times before a connection closed event fires will have no effect.

### Closing Connections

The EOS\_P2P\_CoseConnection function can be used to reject a connection that has been requested, or to close a connection that was previously accepted with a specific user. If all connections with a specific user are closed, the backing socket connection will also be destroyed soon after. The EOS\_P2P\_CoseConnection function requires the following parameters:

| Parameter | Description                                                   |
|-----------|---------------------------------------------------------------|
| Handle    | A valid EOS_HP2P handle received from the Platform Interface. |
| Options   | A pointer to an EOS_P2P_CloseConnectionOptions struct.        |

The EOS\_P2P\_CoseConnectionOptions struct contains the following parameters:

| Parameter    | Description                                                                                      |
|--------------|--------------------------------------------------------------------------------------------------|
| ApiVersion   | Set to EOS_P2P_CLOSECONNECTION_API_LATEST .                                                      |
| LocalUserId  | Set to the EOS_ProductUserId of the local user that is closing or rejecting the connection.      |
| RemoteUserId | Set to the EOS_ProductUserId of the remote user whose connection is being closed or<br>rejected. |
| SocketId     | Pointer to a valid EOS_P2P_SocketId struct you would like to close or reject a connection<br>on. |

The EOS\_P2P\_CoseConnection function will return either a value of EOS\_InvaidParameters , signifying that the supplied input for the function was invalid, or it will return a value of EOS\_Success , in which case the supplied input was valid and the connection will be closed or the connection request will be silently rejected.

The EOS\_P2P\_CoseConnections function can be used to close or reject all connections on a specific SocketId, rather than a connection from a specific user. This could be used at the end of a session to drop all related connections for that session. The EOS\_P2P\_CoseConnections function requires the following parameters:

| Parameter | Description                                                   |
|-----------|---------------------------------------------------------------|
| Handle    | A valid EOS_HP2P handle received from the Platform Interface. |
| Options   | Pointer to an EOS_P2P_CloseConnectionsOptions struct.         |

The EOS\_P2P\_CoseConnectionsOptions struct contains the following parameters:

| Parameter   | Description                                                                                                    |
|-------------|----------------------------------------------------------------------------------------------------------------|
| ApiVersion  | Set to EOS_P2P_CLOSECONNECTIONS_API_LATEST                                                                     |
| LocalUserId | Set to the EOS_ProductUserId of the local user that is closing all connections with the<br>requested SocketId. |
| SocketId    | Pointer to a valid EOS_P2P_SocketId struct that you would like to close all connections on.                    |

The function will return a result of EOS\_InvaidParameters if any of the supplied input was invalid, or it will return EOS\_Success if the supplied information was valid and all the connections with the specified SocketId were successfully closed.

### Receiving Connection Closed Notifications

When an accepted connection that was either open or pending closes, the EOS SDK fires an event that your game can listen for. Note that the EOS SDK does not fire an event for unaccepted connections that close.

To create a handler for connection closed events, use the EOS\_P2P\_AddNotifyPeerConnectionCosed function, which requires the following parameters:

| Parameter               | Description                                                                                                                                                                             |
|-------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Handle                  | A valid EOS_HP2P handle received from the Platform Interface.                                                                                                                           |
| Options                 | Pointer to a EOS_P2P_AddNotifyPeerConnectionClosedOptions struct.                                                                                                                       |
| ClientData (Optional)   | Pointer to data that will be returned to the caller when the event fires.                                                                                                               |
| ConnectionClosedHandler | A function that will be called when the connection closed event fires. This<br>function must take a pointer to an EOS_P2P_OnRemoteConnectionClosedInfo<br>struct passed as a parameter. |

The EOS\_P2P\_AddNotifyPeerConnectionCosedOptions struct contains the following parameters:

| Parameter              | Description                                                                                              |
|------------------------|----------------------------------------------------------------------------------------------------------|
| ApiVersion             | Set to EOS_P2P_ADDNOTIFYPEERCONNECTIONCLOSED_API_LATEST .                                                |
| LocalUserId            | Set to the EOS_ProductUserId of the local user that is listening for connections to<br>close.            |
| SocketId<br>(Optional) | Pointer to a valid EOS_P2P_SocketId struct that you would like to filter connection<br>closed events on. |

The EOS\_P2P\_AddNotifyPeerConnectionCosed function returns an EOS\_NotificationId that can be used to identify the connection closed event handler.

You can remove a connection closed event handler by calling the function EOS\_P2P\_RemoveNotifyPeerConnectionCosed , which takes the following parameters:

| Parameter      | Description                                                                      |
|----------------|----------------------------------------------------------------------------------|
| Handle         | A valid EOS_HP2P handle received from the Platform Interface.                    |
| NotificationId | A valid EOS_NotificationId returned from EOS_P2P_AddNotifyPeerConnectionClosed . |

# Sending and Receiving Data Through P2P Connections

Once a P2P Connection is successfully established, users may send and receive data through it.

### Sending Data

The EOS\_P2P\_SendPacket function will securely send a packet to another user. If there is already an open connection to that peer, it will be sent immediately. If there is not an open connection, then a request for a new connection will be made. The EOS\_P2P\_SendPacket function takes the following parameters:

| Parameter | Description                                                   |
|-----------|---------------------------------------------------------------|
| Handle    | A valid EOS_HP2P handle received from the Platform Interface. |
| Options   | A pointer to a EOS_P2P_SendPacketOptions struct.              |

#### The EOS\_P2P\_SendPacketOptions struct contains the following parameters:

| Parameter             | Description                                                                                                                                                                                                                                                                         |
|-----------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ApiVersion            | Set to EOS_P2P_SENDPACKET_API_LATEST .                                                                                                                                                                                                                                              |
| LocalUserId           | Set to the EOS_ProductUserId of the local user that is sending the<br>packet.                                                                                                                                                                                                       |
| RemoteUserId          | Set to the EOS_ProductUserId of the remote user to send the packet to.                                                                                                                                                                                                              |
| SocketId              | Pointer to a valid EOS_P2P_SocketId .                                                                                                                                                                                                                                               |
| Channel               | Set to the channel to send this data on.                                                                                                                                                                                                                                            |
| DataLengthBytes       | Set to the amount of bytes to send from Data .                                                                                                                                                                                                                                      |
| Data                  | Pointer to the start of the buffer of data to send DataLengthBytes from.                                                                                                                                                                                                            |
| bAllowDelayedDelivery | If we do not have an established connection and this is false, the packet<br>will be silently dropped. Otherwise the packet will be queued until the<br>connection has been opened or until the connection is closed using<br>EOS_P2P_CloseConnection or EOS_P2P_CloseConnections . |
| Reliability           | Sets the reliability of the delivery of this packet. The reliability can be<br>EOS_PR_UnreliableUnordered , EOS_PR_ReliableUnordered , or<br>EOS_PR_ReliableOrdered . If the network connection is interrupted for<br>more than 30 seconds, packets might be lost.                  |

| Parameter                    | Description                                                                                                                                                                                                                                                                                                                                                                                            |
|------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| bDisableAutoAcceptConnection | If this is true , EOS_P2P_SendPacket does not automatically establish<br>a connection with the RemoteUserId , and therefore you must make<br>explicit calls to EOS_P2P_AcceptConnection first, whenever the<br>connection is closed. If this is false , EOS_P2P_SendPacket<br>automatically accepts and starts the connection any time it is called, as<br>long as the connection is not already open. |

This function will return either EOS\_InvaidParameters if any of the supplied input is invalid, or EOS\_Success if the supplied information was valid and could be sent. Note that a return of Success only denotes that the packet can be successfully sent, not whether or not it is successfully delivered. Successful delivery is not guaranteed, as data is sent unreliably.

### Receiving Data

The P2P Interface queues packets that a user receives internally, awaiting a call to the EOS\_P2P\_ReceivePacket function in order to remove it from the queue and store its data in a buffer. This function takes the following parameters:

| Parameter       | Description                                                                                                                                                              |
|-----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Handle          | A valid EOS_HP2P handle received from the Platform Interface.                                                                                                            |
| Options         | A pointer to a valid EOS_P2P_ReceivePacketOptions struct.                                                                                                                |
| OutPeerId       | A pointer to an EOS_ProductUserId value to write out who sent this packet.                                                                                               |
| OutSocketId     | A pointer to an EOS_P2P_SocketId struct to write the associated SocketId data into.                                                                                      |
| OutChannel      | A pointer to a uint8_t to write the associated Channel for this packet.                                                                                                  |
| OutData         | A pointer to a buffer that will have the data from the received packet written to it. This buffer must be the size of MaxDataSizeBytes in the Options for this function. |
| OutBytesWritten | A pointer to a uint32_t , which will record the number of bytes written to OutData .                                                                                     |

The EOS\_P2P\_ReceivePacketOptions struct contains the following parameters:

| Parameter   | Description                                                                               |
|-------------|-------------------------------------------------------------------------------------------|
| ApiVersion  | A version field. Set to EOS_P2P_RECEIVEPACKET_API_LATEST                                  |
| LocalUserId | Set to the EOS_ProductUserId of the local user that is requesting the<br>received packet. |

| Parameter                      | Description                                                                   |
|--------------------------------|-------------------------------------------------------------------------------|
| MaxDataSizeBytes               | The maximum amount of bytes that can be written to the OutData buffer safely. |
| RequestedChannel<br>(Optional) | If set, only packets belonging to this channel will be received.              |

The function will return EOS\_InvaidParameters if any of the input parameters were invalid. It will return EOS\_Success if the packet and its associated data is copied into all of the out-paramters successfully, and it will return EOS\_NotFound if there are no packets available to fulfill the request. It is important to note that the out-parameters will only be written to if the function returns a value of EOS\_Success .

EOS\_P2P\_ReceivePacketOptions requires you to specify the amount of data that can be safely written to the buffer with MaxDataSizeBytes , in addition to specifying the buffer itself with OutData . If the provided buffer is too small to fit the next packet, the data will be silently truncated to the provided buffer's length. It is possible to discard a packet's data entirely by providing a buffer of NULL in OutData , with a size of 0 in MaxDataSizeBytes . It is also possible to safely use a buffer of EOS\_P2P\_MAX\_PACKET\_SIZE to store the data from a packet, but it is recommended to call

EOS\_P2P\_GetNextReceivedPacketSize to retrieve the exact size of the packet data before calling EOS\_P2P\_ReceivePacket . The EOS\_P2P\_MAX\_PACKET\_SIZE is 1170 because of DTLS/SCTP/UDP packet overhead.

It is recommended to call EOS\_P2P\_RecievePacket often to prevent the internal packet queue from filling up. If the internal packet queue is full, incoming packets will be lost rather than received.

### Determining Packet Size

EOS\_P2P\_GetNextReceivedPacketSize retrieves size in bytes of the next packet in the internal queue without removing it from the queue, and it takes the following parameters:

| Parameter          | Description                                                                                              |
|--------------------|----------------------------------------------------------------------------------------------------------|
| Handle             | A valid EOS_HP2P handle received from the Platform Interface.                                            |
| Options            | Pointer to a EOS_P2P_GetNextReceivedPacketSizeOptions struct.                                            |
| OutPacketSizeBytes | A pointer to a uint32_t that can store the size of the next packet for the user calling<br>the function. |

The EOS\_P2P\_GetNextReceivedPacketSizeOptions struct contains the following parameters:

| Parameter  | Description                                                      |
|------------|------------------------------------------------------------------|
| ApiVersion | Set to <code>EOS_P2P_GETNEXTRECEIVEDPACKETSIZE_API_LATEST</code> |

| Parameter                      | Description                                                                                    |
|--------------------------------|------------------------------------------------------------------------------------------------|
| LocalUserId                    | Set to the EOS_ProductUserId of the local user that is requesting the size of the next packet. |
| RequestedChannel<br>(Optional) | If set, only the size of the next packet belonging to this channel will be returned.           |

This function will return a value of EOS\_InvaidParameters if any of the input parameters are invalid, or a value of EOS\_Success if the packet data size was copied to OutPacketSizeBytes successfully. If no data is available, the function will return a value of EOS\_NotFound instead.

# Determining Network Address Translation (NAT) Type

When the P2P Interface attempts to make connections with other players, it attempts NAT-traversal. If the P2P Interface cannot receive an incoming connection because the other player has a restrictive NAT, then the interface automatically uses a relay to establish the peer-to-peer connection between your players. For more information on how the P2P Interface uses relays, see the Relay [Control](#page-11-0) section.

You can call the P2P Interface's EOS\_P2P\_QueryNATType function to retrieve the NAT type of the local player's current connection. The EOS\_P2P\_QueryNATType asynchronously sends multiple packets of data to remote servers, and those servers respond back with what IP address and ports the local game uses. After you know the NAT type of your players, you can use that information to determine how well they can connect to each other.

**Note**: If you make additional calls to EOS\_P2P\_QueryNATType while a request is already in progress, the EOS SDK groups additional calls into the first request; it does not start a new request.

EOS\_P2P\_QueryNATType takes the following parameters:

| Parameter             | Description                                                                                 |
|-----------------------|---------------------------------------------------------------------------------------------|
| Handle                | A valid EOS_HP2P handle received from the Platform Interface.                               |
| Options               | A pointer to an EOS_P2P_QueryNATTypeOptions struct.                                         |
| ClientData (Optional) | Pointer to data that EOS_P2P_QueryNATType returns to the caller when the<br>query finishes. |
| NATTypeQueriedHandler | Pointer to a function to call when the query finishes.                                      |

The EOS\_P2P\_QueryNATTypeOptions struct contains the following parameters:

| Parameter  | Description                                                    |
|------------|----------------------------------------------------------------|
| ApiVersion | A version field. Set this to EOS_P2P_QUERYNATTYPE_API_LATEST . |

EOS\_P2P\_QueryNATType does not return a value directly because it is asynchronous. Instead, EOS\_P2P\_QueryNATType outputs the player's NAT type through the callback function that you set for the NATTypeQueriedHander parameter.

The P2P Interface represents the NAT type with EOS\_ENATType , which has the following possible values:

| Value            | Description                                                                                                                        |
|------------------|------------------------------------------------------------------------------------------------------------------------------------|
| EOS_NAT_Unknown  | The local player's NAT type is either currently unknown or the P2P Interface is unable to<br>accurately determine it.              |
| EOS_NAT_Open     | All types of peers should be able to directly connect to the local player.                                                         |
| EOS_NAT_Moderate | The local player can directly connect to other moderate and open peers. Relay servers<br>are not required.                         |
| EOS_NAT_Strict   | The local player can directly connect only to open peers. To connect to other peers, the<br>local player might need to use relays. |

### Get the Nat Type From the Cache

After EOS\_P2P\_QueryNATType completes at least once, the EOS SDK caches the value of the player's NAT type. You can then call the function EOS\_P2P\_GetNATType to return the player's NAT type value immediately. To call the EOS\_P2P\_GetNATType function, you must provide the following parameters:

| Parameter  | Description                                                                                                          |
|------------|----------------------------------------------------------------------------------------------------------------------|
| Handle     | A valid EOS_HP2P handle received from the Platform Interface.                                                        |
| Options    | Pointer to an EOS_P2P_GetNATTypeOptions struct.                                                                      |
| OutNATType | Pointer to an EOS_ENATType object to set to the cached value that was previously queried by<br>EOS_P2P_QueryNATType. |

The EOS\_P2P\_GetNATTypeOptions struct contains the following parameters:

| Parameter  | Description                            |
|------------|----------------------------------------|
| ApiVersion | Set to EOS_P2P_GETNATTYPE_API_LATEST . |

The EOS\_P2P\_GetNATType function returns EOS\_Success if EOS\_P2P\_QueryNATType was successfully called in the past. It returns EOS\_NotFound if EOS\_P2P\_QueryNATType has not been called before, or if it has not completed.

### <span id="page-11-0"></span>Relay Control

The P2P Interface's relay control setting determines if the P2P Interface uses relays to establish the peer-to-peer connection. Each platform, except Xbox, has three relay modes available. For more information on the Xbox-specific peer-to-peer requirements, see the EOS SDK for Xbox: Using [Peer-to-peer](https://dev.epicgames.com/docs/en-US/epic-online-services/platforms/xbox#using-peer-to-peer-p2p) documentation.

You can only access console documentation if you have the appropriate permissions. See the Get [Started](https://dev.epicgames.com/docs/en-US/epic-online-services/eos-get-started/services-quick-start#eos-sdk-download-types) Steps: EOS SDK [Download](https://dev.epicgames.com/docs/en-US/epic-online-services/eos-get-started/services-quick-start#eos-sdk-download-types) Types documentation for more information on how to access the EOS SDKs for consoles and their associated documentation.

**Note**: Our relays are hosted in AWS data centers by Amazon. The specific relay server that a player connects to depends on Amazon's GEOIP DNS routing.

The table below lists the available relay modes for the P2P Interface:

| Mode            | Description                                                                                                                                                                                                                                                                                                                                                                                                  |
|-----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Allow<br>Relays | First, the P2P Interface attempts a direct peer-to-peer connection between players through a Session<br>Traversal Utilities for NAT (STUN) server. The STUN server reports what the players' IP addresses and<br>ports are, so the P2P Interface can establish the connection. If a direct connection through the STUN<br>server fails, the P2P Interface attempts to use relays. This is the default value. |
| No<br>Relays    | The P2P Interface doesn't use relays to attempt to establish a peer-to-peer connection. It attempts<br>only a direct peer-to-peer connection (through a STUN server). Players with restrictive NATs might not<br>be able to connect to other players with this mode.                                                                                                                                         |
| Force<br>Relays | The P2P Interface uses only relays to attempt to establish the peer-to-peer connection. This adds<br>latency to all connections, but it hides your players' IP Addresses from each other.                                                                                                                                                                                                                    |

See the [EOS\\_EReayContro](https://dev.epicgames.com/docs/en-US/api-ref/enums/eos-e-relay-control) documentation for more information.

**Note**: Your players' firewalls must allow UDP packets through to use the relay connection; typically, firewalls allow UDP packets through. However, if a player's firewall is restrictive, it might still block UDP packets, or it might require specific whitelisting of ports to allow UDP packets through. In either of these cases, the relay fails.
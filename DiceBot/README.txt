DiceBotProject is the client side of a simple dice-rolling project. As long as the designated server is open, the client requests dice rolls or statistical reports on previous dice rolls. All data passed between the client and the server is formatted as JSON. 

Ultimately, this part of the project is incomplete. It was intended to be a Discord bot, and much of the code is still set up under that assumption. Unfortunately, testing suggests that running requests through two HTTPS connexions rather than just one would be far too slow, especially given Discord's inherent delays in processing application commands. As such, Discord integration will not be implemented.

In any case, this project is perfectly capable of functioning as a remote dice-rolling application, although the server is currently only designed to accept a single client at a time.

The first line of the file .token stores the last known session token issued to this client by the server.

The configuration file .config stores persistent settings for the application. All settings must be expressed as <name>:<value>, with no leading spaces in the name or value. If a setting is not recorded correctly, the app will simply use the default value.

Current settings are:
ServerIP: The IP address of the server to which the client will connect. Default value: "127.0.0.1" (localhost).
ServerPort: The port number of the server. Default value: 50023.
ClientPort: The port number that the client will use. Default value: 49223.
ClientReadTimeout: The default client read timeout when waiting for client messages, in milliseconds. Default value: 5000.
ClientWriteTimeout: The default client write timeout when sending messages, in milliseconds. The write timeout is lengthened when waiting for user interactions, such as typing a password. Default value: 5000.
ServerCN: The common name (CN) of the server. Default value: "Mono Test Root Agency".
ClientUsername: The client's username. Default value: "Discord Bot".
ServerThumbprint: The server's certificate's thumbprint. This can be used to authenticate a specific, known, self-signed certificate, even if the certificate would otherwise be rejected. Default value: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA".
BotToken: The Discord application's secret token. This value is necessary to interact with Discord's API as a bot. Default value (dummy value): "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA".

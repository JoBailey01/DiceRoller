DiceLogProject is the server side of a simple dice-rolling project. When started, the server authenticates clients (known as 'admins'), rolls dice, and records their dice rolls in a SQLite database. All data passed between the client and the server is formatted as JSON. The database is structured to store the metadata from Discord's application commands.

Start the executable file with -n or -N to add a new user instead of starting the server.

The configuration file .config stores persistent settings for the application. All settings must be expressed as <name>:<value>, with no leading spaces in the name or value. If a setting is not recorded correctly, the app will simply use the default value.

Current settings are:
TokenTimeout: How long, in seconds, a session token remains valid after it is issued. Default value: 172800 (48 hours).
ServerPort: The default port number that the server will use. Default value: 50023.
CertificateFile: The name of the file containing the X509 certificate for the server. Default value: "cert.pfx". This file name must not contain a colon.
CertificatePassword: The password associated with the server's certificate. Default value: "password1234". This value should not contain a colon.
DatabaseFile: The name of the SQLite database file. Default value: "dicelog.db". This file name must not contain a colon.
ServerReadTimeout: The default server read timeout when waiting for client messages, in milliseconds. The read timeout is lengthened when waiting for user interactions, such as typing a password. Default value: 5000.
ServerWriteTimeout: The default server write timeout when sending messages, in milliseconds. Default value: 5000.

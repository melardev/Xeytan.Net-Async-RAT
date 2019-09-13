# WARNING
Even though this is one of my old personal projects, it is still in the early phases of development.
Not stable nor production ready.

# Introduction
A RAT application built in CSharp. I tried to use asynchronous APIs as much as I could, so at this
moment in time I used Async Socket API and Event based architecture for communication between subsystems(Net and Ui).

# Features
- Console Based(For now, in the future it will have UI)
- Process List
- Explore directories
- Desktop streaming
- Reverse Shell
- Get client's system information

# TODO
- Encryption
- Before changing directory, I have to implement directory exists checking, I should be using
PacketFileSystem.FileSystemFocus.DirectoryExists enumerator
- Review the thread safety, mainly in the Ui, When we press Ctrl + C the callback is called in
another thread, and in that callback I make some changes to properties, is my implementation thread safe?
- Ctr + C callback is only triggered once, fix it.
- Camera Streaming
- Graphical User Interface
- Ctr + C callback is only triggered once, fix it.
- UiMediator has to be refactored, the locking is not robust, I have to create another lock for
The CurrentViewChanges.
- There is a race condition if one packet is received before the previous one gets handled,
this is an unlikely situation, so I may not fix it. For example: If I receive a PacketShell to begin
a reverse shell session and before spawning that shell, I get another PacketShell with a command to be
executed this second packet and if the shell was not spawn yet, then the command won't be executed.
This should never occur in real world scenarios. I may or may not fix this ... (For example queuing
packets in another thread which processes packets one by one taken from a queue so they are not lost).
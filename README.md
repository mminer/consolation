Unity's editor console is indispensible, but retrieving debug logs and warnings
outside the editor is difficult. To make it easier, this console displays
output from the `Debug` class, as well as any errors and exceptions thrown, in
the game itself. This is especially useful on mobile devices.


## Using

Attach this script to a game object. The window can be opened with a
configurable hotkey (defaults to backtick). Alternatively, enable shake-to-open
in the Inspector to open the console on mobile devices.


## Compatibility

Supports Unity 4 and 5. It hasn't been tested on all the platforms that Unity
supports but it probably works on most.

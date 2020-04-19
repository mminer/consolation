# Consolation

Unity's editor console is indispensible, but retrieving debug logs and warnings
outside the editor is difficult. To make it easier, this console displays
output from the `Debug` class, as well as any errors and exceptions thrown, in
the game itself. This is especially useful on mobile devices.

![Screenshot](http://matthewminer.com/images/consolation@2x.png)


## Using

Attach this script to a game object. The window can be opened with a
configurable hotkey (defaults to backtick). Alternatively, enable shake-to-open
in the Inspector to open the console on mobile devices (with an option to
prevent accidental shakes by requiring 3 or more fingers on the screen).


## Compatibility

Supports Unity 5, 2017.x, 2018.x, and 2019.x. It hasn't been tested on all the
platforms that Unity supports but it probably works on most.

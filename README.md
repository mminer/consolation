# Consolation

Unity's editor console is indispensible, but retrieving debug logs and warnings
outside the editor is difficult. To make it easier, this console displays output
from `Debug` in the game itself. This is especially useful on mobile devices.

![Screenshot](http://matthewminer.com/images/consolation@2x.png)


## Installing

### Copy Script

The `Console` component is entirely self-contained in *Console.cs*, so
installation is as simple as dragging this script into your project.

### Unity Package Manager

Alternatively, add the package to your project via
[UPM](https://docs.unity3d.com/Manual/upm-ui.html) using the Git URL
https://github.com/mminer/consolation.git. You can also clone the repository and
point UPM to your local copy.


## Using

Attach the `Console` component to a game object. When playing your game, open
the console window with the back quote key <kbd>`</kbd>. This shortcut is
configurable in the inspector.

Alternatively, enable shake-to-open in the inspector to open the console on
mobile devices. The component provides an option to prevent accidental shakes by
requiring 3 or more fingers on the screen.

Several other settings like font size and the maximum log count are also
configurable in the inspector.


## Compatibility

Supports Unity 2017.x and above. It hasn't been tested on all the platforms that
Unity supports but it probably works on most.

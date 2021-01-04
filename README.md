# Consolation

It's difficult to retrieve logs and warnings from Unity outside the editor.  To
make it easier, this console displays output from `Debug` in the game itself.
This is especially useful on mobile devices.

![Console in Unity game](http://matthewminer.com/images/consolation@2x.png#2)


## Installing

### Copy Script

The `Console` component is entirely self-contained in *Console.cs*, so
installation is as simple as dragging this script into your project.

### Unity Package Manager

Alternatively, add the package to your project via
[UPM](https://docs.unity3d.com/Manual/upm-ui.html) using the Git URL
https://github.com/mminer/consolation.git. You can also clone the repository and
point UPM to your local copy.

![Adding package to UPM](http://matthewminer.com/images/consolation-upm.gif)


## Using

Attach the `Console` component to a game object. When playing your game, open
the console window with the back quote key <kbd>`</kbd>. This shortcut is
configurable in the inspector.

Alternatively, enable shake-to-open in the inspector to open the console on
mobile devices. The component provides an option to prevent accidental shakes by
requiring 3 or more fingers on the screen.

Several other settings like font size and the maximum log count are also
configurable in the inspector.

![Console component
inspector](http://matthewminer.com/images/consolation-inspector.png)


## Compatibility

Supports Unity 2017.x and above. It hasn't been tested on all the platforms that
Unity supports but it probably works on most.

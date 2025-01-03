# Time to Stop
This repository is formatted such that it can be used with the [malware-dev's MDK^2](https://github.com/malforge/mdk2).
The only necessary file is the `Program.cs` file, which contains code that will be put in a programmable block.

# Installation
The contents of the script **inside** and **not including** the brackets that define `Program` should be copied and pasted inside a programmable block.

# Usage
The script is told how to function using the **custom data**.
Inside the custom data there should be two lines. The first will be a cockpit, remote control, or cryopod that provides the velocity data.
The second will be a semicolon-separated list of displays with a specific format.

### First Line
The name as seen in the control panel. For example, "Control Seat".
Example line:
`Remote Control`

### Second line
The semicolon-separated list of displays to output to. It works with blocks that have multiple displays and single displays.
There is an additional parameter, that tells the script whether to display a longer output or a shorter output.
Format for ONE display: `Block Name:l` or `Block Name:s`. 
The format for a multi-display is more complicated. Inside the block, it should have a list of displays. The first display in the list is numbered 0.
You point the script to the block and then the number of the display, like so:
`Block Name:0:s` to target the first display of block "Block Name" in short mode.

### Example:
```
Control Seat
Control Seat:0:l
Control Seat:1:s
LCD Display:l
```

## Reading the output
It uses your last measured deceleration to calculate the distance and time to stop. This means it won't work until you have started decelerating.
This is a little odd to get used to, but it is highly accurate and provides results within ~2 meters (depending on how heavy your ship is). The inaccuracy is due to the deceleration curve, which is constant until the last few meters, where it becomes linear[^1]
[^1]: Maybe linear, maybe logarithmic, maybe quadratic. I don't really know, but your deceleration does reduce significantly.

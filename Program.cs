using Sandbox.Game.Entities.Interfaces;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.
        private class CircularBuffer<T> : IEnumerable<T> 
        {
            private T[] _buf;

            public CircularBuffer(int capacity) 
            {
                _buf = new T[capacity];
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new CircularEnumerator(_buf); 
            }

            public void Push(T item) 
            {
                // Shift the array down
                for (int i = _buf.Length - 1; i > 0; i--)
                {
                    _buf[i] = _buf[i - 1];
                }

                _buf[0] = item;
            }

            public T GetValueAt(int idx)
            {
                if (idx < 0 || idx >= _buf.Length) throw new Exception("Can't access index " + idx + " for array of size " + _buf.Length);
                return _buf[idx];
            }

            public int Length
            {
                get { return _buf.Length; }
            }

            public T Tail
            {
                get { return _buf[_buf.Length - 1]; }
            }

            public T Head
            {
                get { return _buf[0]; }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            class CircularEnumerator : IEnumerator<T> 
            {
                private readonly T[] _collection;
                private int curIdx;
                private T curObj;

                internal CircularEnumerator(T[] collection) 
                {
                    _collection = collection;
                }

                public bool MoveNext() 
                {
                    if (++curIdx >= _collection.Length) return false;
                    curObj = _collection[curIdx];
                    return true;
                }

                public void Reset() 
                {
                    curIdx = -1;
                }

                void IDisposable.Dispose()
                {

                }

                public T Current 
                {
                    get { return curObj; }
                }

                object IEnumerator.Current
                {
                    get { return Current; }
                }
            }
        }

        private class PreferentialTextSurface
        {
            private bool _isShort;
            private IMyTextSurface _textSurface;

            internal bool Short
            {
                get { return _isShort; }
            }

            internal IMyTextSurface LCD
            {
                get { return _textSurface; }
            }

            internal PreferentialTextSurface(IMyTextSurface ts, bool isShort)
            {
                _isShort = isShort;
                _textSurface = ts;
            }
        }

        private static readonly CircularBuffer<double> Velocities = new CircularBuffer<double>(6);
        private static List<PreferentialTextSurface> PreferentialLCDs = new List<PreferentialTextSurface>();
        private static IMyShipController MyShipController;
        private static string[] args;

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 

            // Each tick is 1/60 of a second. This script updates every 1/6 second.
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            // Load args
            args = Me.CustomData.Split('\n');
            if (args.Length != 2) Echo("Arguments are of wrong length! There should be 2 arguments!");

            // Get the ship controller
            List<IMyShipController> myShipControllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(myShipControllers, sc => sc.CustomName.Contains(args[0]));
            MyShipController = myShipControllers.ElementAt(0);

            // Get the display(s)
            string[] displays = args[1].Split(';');
            PreferentialTextSurface curDisplay;

            foreach(string display in displays) 
            {
                // Determine if each specified display is a text surface or single lcd panel.
                // The syntax for a single text surface is as such: LCDPanel:l
                // For a multi text surface: LCDPanels:0:l

                string[] spl = display.Split(':');

                if (spl.Length == 3) // Case where it is a multi LCD
                {
                    string target = spl[0];
                    int targetIdx = Convert.ToInt32(spl[1]);
                    bool prefersShort = Convert.ToString(spl[2]).ToLower() != "l";

                    IMyTextSurfaceProvider tsp = GridTerminalSystem.GetBlockWithName(target) as IMyTextSurfaceProvider;
                    if (tsp == null) 
                    {
                        Echo("Couldn't find text surface provider " + target + "!");
                        continue;
                    }

                    curDisplay = new PreferentialTextSurface(tsp.GetSurface(targetIdx), prefersShort);
                    if (curDisplay.LCD == null) 
                    {
                        Echo("Couldn't find display " + targetIdx + " for text surface provider " + tsp + "!");
                        continue;
                    }
                } else // Case where it is one display
                {
                    string target = spl[0];
                    bool prefersShort = Convert.ToString(spl[1]).ToLower() != "l";

                    IMyTextSurface ts = GridTerminalSystem.GetBlockWithName(target) as IMyTextSurface;
                    curDisplay = new PreferentialTextSurface(ts, prefersShort);
                    if (curDisplay.LCD == null) 
                    {
                        Echo("Couldn't find display " + display + "!");
                        continue;
                    }
                }
                
                PreferentialLCDs.Add(curDisplay);
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            // Fetch current velocity
            // Fetch average deceleration over the last 5(?) seconds, if positive then acceleration

            // V_0 / D = s <- How many seconds until 0 velocity?
            // V_0 * s - 1/2 D * s^2 <- How much distance covered in s time?
            double secondsUntilStill, distanceToStop;

            double currentVelocity = MyShipController.GetShipSpeed();
            Velocities.Push(currentVelocity);
            double avgDeceleration = -GetRecentDeceleration();
            Echo("Delta V per second: " + avgDeceleration);

            if (avgDeceleration > 0.1) 
            {
                secondsUntilStill = Math.Round(currentVelocity / avgDeceleration, 1);
                distanceToStop = (secondsUntilStill * currentVelocity) - (0.5 * avgDeceleration * secondsUntilStill * secondsUntilStill);
            } else 
            {
                secondsUntilStill = 0;
                distanceToStop = 0;
            }

            Echo("Seconds Until Standstill: " + secondsUntilStill);
            Echo("Distance to Stop: " + distanceToStop);

            // Numbers acquired. Put them on an LCD
            StringBuilder shortString = new StringBuilder();
            StringBuilder longString = new StringBuilder();
            shortString.Append("T-" + secondsUntilStill).AppendLine()
                        .Append("M-" + distanceToStop);
            longString.Append("Seconds Until Standstill: ").Append(secondsUntilStill).AppendLine()
                        .Append("Distance to Stop: " + distanceToStop);

            foreach (PreferentialTextSurface PrefDisplay in PreferentialLCDs) 
                PrefDisplay.LCD.WriteText(PrefDisplay.Short ? shortString : longString);
        }

        private double GetRecentDeceleration(int recency = 1) 
        {
            // The Recent Velocity will be the head velocity subtracted from the velocity at the recency index,
            // divided by whatever the second-interval is to get a value in meters per second.
            // The second-interval is 1/6, so the time_increment should be 6.
            // We then have to take recency into account; in other words, how much will that second-interval be
            // skewed by. To account for this, divide by the recency.
            // In other words, V_recent = (V_head - V_recency) * time_increment / recency
            return (Velocities.Head - Velocities.GetValueAt(recency)) * 6 / recency;
        }
    }
}

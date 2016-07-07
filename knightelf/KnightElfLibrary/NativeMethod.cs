/****************************** Module Header ******************************\
* Module Name:	NativeMethod.cs
* Project:		CSWindowsHook
* Copyright (c) Microsoft Corporation.
* 
* The P/Invoke signatures of some native APIs.
* 
* This source is subject to the Microsoft Public License.
* See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL.
* All other rights reserved.
* 
* History:
* * 4/6/2009 10:57 AM Jialiang Ge Created
\***************************************************************************/

#region Using directives
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
#endregion

namespace KnightElfLibrary
{
    /// <summary>
    /// Native methods
    /// </summary>
    public class NativeMethod
    {
        /// <summary>
        /// Get current thread ID.
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        /// <summary>
        /// Get current process ID.
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();

        /// <summary>
        /// Inject events in the system queue
        /// </summary>
        /// <param name="numberOfInputs">The number of events</param>
        /// <param name="input">The event</param>
        /// <param name="structSize">The size of the event struct</param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern uint SendInput(uint numberOfInputs, [MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] KnightElfLibrary.InputMessage.INPUT[] input, int structSize);

        /// <summary>
        /// Struct representing a point.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <see>See MSDN documentation for further information.</see>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            //bool success = User32.GetCursorPos(out lpPoint);
            // if (!success)

            return lpPoint;
        }
    }
}
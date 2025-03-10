﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2021 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Threading;

namespace ShareX
{
    public class CaptureWindow : CaptureBase
    {
        public IntPtr WindowHandle { get; private set; }

        public CaptureWindow(IntPtr windowHandle)
        {
            WindowHandle = windowHandle;

            AllowAutoHideForm = WindowHandle != Program.MainForm.Handle;
        }

        protected override TaskMetadata Execute(TaskSettings taskSettings)
        {
            WindowInfo windowInfo = new WindowInfo(WindowHandle);

            if (windowInfo.IsMinimized)
            {
                windowInfo.Restore();
            }

            windowInfo.Activate();

            Thread.Sleep(250);

            TaskMetadata metadata = new TaskMetadata();
            metadata.UpdateInfo(windowInfo);

            if (taskSettings.CaptureSettings.CaptureTransparent && !taskSettings.CaptureSettings.CaptureClientArea)
            {
                metadata.Image = TaskHelpers.GetScreenshot(taskSettings).CaptureWindowTransparent(WindowHandle);
            }
            else
            {
                metadata.Image = TaskHelpers.GetScreenshot(taskSettings).CaptureWindow(WindowHandle);
            }

            return metadata;
        }
    }
}
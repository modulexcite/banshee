/***************************************************************************
 *  QuicktimePlayerEngine.cs
 *
 *  Written by Scott Peterson <scottp@gnome.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections;
using System.Timers;
using QTOControlLib;
using QTOLibrary;
using AxQTOControlLib;
using Mono.Unix;

using Banshee.MediaEngine;

public static class PluginModuleEntry
{
    public static Type [] GetTypes()
    {
        return new Type [] {
            typeof(Banshee.MediaEngine.Quicktime.QuicktimePlayerEngine)
        };
    }
}

namespace Banshee.MediaEngine.Quicktime
{
    
    public class QuicktimePlayerEngine : PlayerEngine
    {
        private readonly Timer timer = new Timer(500);
        private readonly ActiveXControl axcontrol = new ActiveXControl();
        
        public QuicktimePlayerEngine()
        {
            if(!QTControl.get_IsQuickTimeAvailable(0)) {
                throw new ApplicationException(Catalog.GetString("Quicktime is not availible"));
            }
            if(QTControl.QuickTimeInitialize(0, 0) != 0) {
                throw new ApplicationException(Catalog.GetString("Quicktime could not be initialized"));
            }

            QTControl.QTEvent += new AxQTOControlLib._IQTControlEvents_QTEventEventHandler(QTControl_QTEvent);
            QTControl.Error += new AxQTOControlLib._IQTControlEvents_ErrorEventHandler(QTControl_Error);

            timer.AutoReset = true;
            timer.Enabled = false;
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
        }

        void QTControl_Error(object sender, _IQTControlEvents_ErrorEvent e)
        {
            Console.WriteLine("Quicktime Error: " + e.errorCode);
            
            OnEventChanged(PlayerEngineEvent.Error);
            Close();
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnEventChanged(PlayerEngineEvent.Iterate);
        }

        void QTControl_QTEvent(object sender, _IQTControlEvents_QTEventEvent e)
        {
            if(e.eventID == (int)QTEventIDsEnum.qtEventMovieDidEnd) {
                // You can't change the QTMovie from within the event handler. Booo.
                Banshee.Base.ThreadAssist.Spawn(delegate {
                    Close();
                    OnEventChanged(PlayerEngineEvent.EndOfStream);
                });
            }
        }

        public override void Dispose()
        {
            Close();
            timer.Dispose();
            axcontrol.Dispose();
            QTControl.QuickTimeTerminate();
        }

        protected override void OpenUri(Banshee.Base.SafeUri uri)
        {
            QTControl.URL = uri.LocalPath;
            QTControl.Movie.EventListeners.Add(QTEventClassesEnum.qtEventClassStateChange, QTEventIDsEnum.qtEventMovieDidEnd, 0, null);
        }

        public override void Play()
        {
            QTControl.Movie.Play(1);
            timer.Start();
            base.Play();
        }

        public override void Pause()
        {
            timer.Stop();
            QTControl.Movie.Pause();
            base.Pause();
        }

        public override void Close()
        {
            timer.Stop();
            QTControl.URL = "";
            base.Close();
        }

        public override ushort Volume {
            get {
                return QTControl.Movie != null
                    ? (ushort)(QTControl.Movie.AudioVolume * 100)
                    : (ushort)0;
            }
            set {
                if(QTControl.Movie != null) {
                    QTControl.Movie.AudioVolume = (float)value / (float)100;
                }
            }
        }

        public override uint Position {
            get {
                return QTControl.Movie != null
                  ? (uint)(QTControl.Movie.Time / QTControl.Movie.TimeScale)
                  : 0;
            }
            set {
                if(QTControl.Movie != null) {
                    QTControl.Movie.Time = (int)(value * QTControl.Movie.TimeScale);
                }
            }
        }

        public override uint Length {
            get {
                return QTControl.Movie != null
                    ? (uint)(QTControl.Movie.Duration / QTControl.Movie.TimeScale)
                    : 0; }
        }

        private static string[] source_capabilities = { "file" };
        public override IEnumerable SourceCapabilities {
            get { return source_capabilities; }
        }

        private static string[] decoder_capabilities = { "m4p", "m4a" };
        public override IEnumerable ExplicitDecoderCapabilities {
            get { return decoder_capabilities; }
        }

        public override string Id {
            get { return "quicktime"; }
        }

        public override string Name {
            get { return Catalog.GetString("Quicktime"); }
        }
        
        private AxQTControl QTControl {
            get { return axcontrol.axQTControl; }
        }
    }
}

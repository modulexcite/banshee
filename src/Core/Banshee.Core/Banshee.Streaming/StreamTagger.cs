//
// StreamTagger.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Text.RegularExpressions;

using Banshee.IO;
using Banshee.Base;
using Banshee.Collection;

namespace Banshee.Streaming
{
    public static class StreamTagger
    {
        // This is a short list of video types that TagLib# might not support but that
        // we want to make sure are recognized as videos
        private static readonly ExtensionSet VideoExtensions = new ExtensionSet (
            "avi", "divx", "dv", "f4p", "f4v", "flv", "m4v", "mkv", "mov", "ogv", "qt");
        
        public static TagLib.File ProcessUri (SafeUri uri)
        {
            try {
                TagLib.File file = Banshee.IO.DemuxVfs.OpenFile (uri.IsLocalPath ? uri.LocalPath : uri.AbsoluteUri, 
                    null, TagLib.ReadStyle.Average);
    
                if ((file.Properties.MediaTypes & TagLib.MediaTypes.Audio) == 0 && 
                    (file.Properties.MediaTypes & TagLib.MediaTypes.Video) == 0) {
                    throw new TagLib.UnsupportedFormatException ("File does not contain video or audio");
                }
                return file;
            } catch (Exception) {
                return null;
            }
        }

        private static string Choose (string priority, string fallback)
        {
            return Choose (priority, fallback, false);
        }
    
        private static string Choose (string priority, string fallback, bool flip)
        {
            return flip 
                ? String.IsNullOrEmpty (fallback) ? priority : fallback
                : String.IsNullOrEmpty (priority) ? fallback : priority;
        }

        #pragma warning disable 0169
        
        private static int Choose (int priority, int fallback)
        {
            return Choose (priority, fallback, false);
        }
        
        #pragma warning restore 0169
        
        private static int Choose (int priority, int fallback, bool flip)
        {
            return flip 
                ? (fallback <= 0 ? priority : fallback)
                : (priority <= 0 ? fallback : priority);
        }
        
        private static void FindTrackMediaAttributes (TrackInfo track, TagLib.File file)
        {
            track.MediaAttributes = track.MediaAttributes;
            
            if ((file.Properties.MediaTypes & TagLib.MediaTypes.Audio) != 0) {
                track.MediaAttributes |= TrackMediaAttributes.AudioStream;
            }
            
            if ((file.Properties.MediaTypes & TagLib.MediaTypes.Video) != 0) {
                track.MediaAttributes |= TrackMediaAttributes.VideoStream;
            }
            
            // TODO: Actually figure out, if possible at the tag/file level, if
            // the file is actual music, podcast, audiobook, movie, tv show, etc.
            // For now just assume that if it's only audio, it's music, since that's
            // what we've just historically assumed on any media type
            if ((track.MediaAttributes & TrackMediaAttributes.VideoStream) == 0) {
                track.MediaAttributes |= TrackMediaAttributes.Music;
            }
        }
        
        public static void TrackInfoMerge (TrackInfo track, TagLib.File file)
        {
            TrackInfoMerge (track, file, false);
        }

        public static void TrackInfoMerge (TrackInfo track, TagLib.File file, bool preferTrackInfo)
        {
            // TODO support these as arrays:
            // Performers[] (track artists), AlbumArtists[], Composers[], Genres[]

            // Note: this should be kept in sync with the metadata written in SaveTrackMetadataJob.cs

            if (file != null) {
                track.Uri = new SafeUri (file.Name);
                track.MimeType = file.MimeType;
                track.BitRate  = file.Properties.AudioBitrate;
                track.Duration = file.Properties.Duration;
                
                FindTrackMediaAttributes (track, file);
    
                track.ArtistName = Choose (file.Tag.JoinedPerformers, track.ArtistName, preferTrackInfo);
                track.AlbumTitle = Choose (file.Tag.Album, track.AlbumTitle, preferTrackInfo);
                track.AlbumArtist = Choose (file.Tag.FirstAlbumArtist, track.AlbumArtist, preferTrackInfo);
                track.IsCompilation = IsCompilation (file.Tag);
                
                track.TrackTitle = Choose (file.Tag.Title, track.TrackTitle, preferTrackInfo);
                track.Genre = Choose (file.Tag.FirstGenre, track.Genre, preferTrackInfo);
                track.Composer = Choose (file.Tag.FirstComposer, track.Composer, preferTrackInfo);
                track.Conductor = Choose (file.Tag.Conductor, track.Conductor, preferTrackInfo);
                track.Grouping = Choose (file.Tag.Grouping, track.Grouping, preferTrackInfo);
                track.Copyright = Choose (file.Tag.Copyright, track.Copyright, preferTrackInfo);
                track.Comment = Choose (file.Tag.Comment, track.Comment, preferTrackInfo);
    
                track.TrackNumber = Choose ((int)file.Tag.Track, track.TrackNumber, preferTrackInfo);
                track.TrackCount = Choose ((int)file.Tag.TrackCount, track.TrackCount, preferTrackInfo);
                track.DiscNumber = Choose ((int)file.Tag.Disc, track.DiscNumber, preferTrackInfo);
                track.DiscCount = Choose ((int)file.Tag.DiscCount, track.DiscCount, preferTrackInfo);
                track.Year = Choose ((int)file.Tag.Year, track.Year, preferTrackInfo);
                track.Bpm = Choose ((int)file.Tag.BeatsPerMinute, track.Bpm, preferTrackInfo);
            } else {
                track.MediaAttributes = TrackMediaAttributes.AudioStream;
                if (VideoExtensions.IsMatchingFile (track.Uri.LocalPath)) {
                    track.MediaAttributes = TrackMediaAttributes.VideoStream;
                }
            }

            track.FileSize = Banshee.IO.File.GetSize (track.Uri);
            track.FileModifiedStamp = Banshee.IO.File.GetModifiedTime (track.Uri);
            track.LastSyncedStamp = DateTime.Now;

            if (String.IsNullOrEmpty (track.TrackTitle)) {
                try {
                    string filename = System.IO.Path.GetFileNameWithoutExtension (track.Uri.LocalPath);
                    if (!String.IsNullOrEmpty (filename)) {
                        track.TrackTitle = filename;
                    }
                } catch {}
            }
            
            // TODO look for track number in the file name if not set?
            // TODO could also pull artist/album from folders _iff_ files two levels deep in the MusicLibrary folder
            // TODO these ideas could also be done in an extension that collects such hacks
        }
            
        private static bool IsCompilation (TagLib.Tag tag)
        {
            TagLib.Id3v2.Tag id3v2_tag = tag as TagLib.Id3v2.Tag;
            if (id3v2_tag != null && id3v2_tag.IsCompilation)
                return true;

            TagLib.Mpeg4.AppleTag apple_tag = tag as TagLib.Mpeg4.AppleTag;
            if (apple_tag != null && apple_tag.IsCompilation)
                return true;
            
            if (tag.Performers.Length > 0 && tag.AlbumArtists.Length > 0 &&
                (tag.Performers.Length != tag.AlbumArtists.Length || tag.FirstAlbumArtist != tag.FirstPerformer)) {
                return true;
            }
            return false;
        }
    
        public static void TrackInfoMerge (TrackInfo track, StreamTag tag)
        {
            try {
                switch (tag.Name) {
                    case CommonTags.Artist:
                        track.ArtistName = Choose ((string)tag.Value, track.ArtistName);
                        break;
                    case CommonTags.Title:
                        //track.TrackTitle = Choose ((string)tag.Value, track.TrackTitle);
                        string title = Choose ((string)tag.Value, track.TrackTitle);

                        // Try our best to figure out common patterns in poor radio metadata.
                        // Often only one tag is sent on track changes inside the stream, 
                        // which is title, and usually contains artist and track title, separated
                        // with a " - " string.
                        if (track.IsLive && title.Contains (" - ")) {
                            string [] parts = Regex.Split (title, " - ");
                            track.TrackTitle = parts[1].Trim ();
                            track.ArtistName = parts[0].Trim ();

                            // Often, the title portion contains a postfix with more information
                            // that will confuse lookups, such as "Talk [Studio Version]".
                            // Strip out the [] part.
                            Match match = Regex.Match (track.TrackTitle, "^(.+)[ ]+\\[.*\\]$");
                            if (match.Groups.Count == 2) {
                                track.TrackTitle = match.Groups[1].Value;
                            }
                        } else {
                            track.TrackTitle = title;
                        }
                        break;
                    case CommonTags.Album:
                        track.AlbumTitle = Choose ((string)tag.Value, track.AlbumTitle);
                        break;
                    case CommonTags.Disc:
                    case CommonTags.AlbumDiscNumber:
                        int disc = (int)tag.Value;
                        track.DiscNumber = disc == 0 ? track.DiscNumber : disc;
                        break;
                    case CommonTags.AlbumDiscCount:
                        int count = (int)tag.Value;
                        track.DiscCount = count == 0 ? track.DiscCount : count;
                        break;
                    case CommonTags.Genre:
                        track.Genre = Choose ((string)tag.Value, track.Genre);
                        break;
                    case CommonTags.Composer:
                        track.Composer = Choose ((string)tag.Value, track.Composer);
                        break;
                    case CommonTags.Copyright:
                        track.Copyright = Choose ((string)tag.Value, track.Copyright);
                        break;
                    case CommonTags.LicenseUri:
                        track.LicenseUri = Choose ((string)tag.Value, track.LicenseUri);
                        break;
                    case CommonTags.Comment:
                        track.Comment = Choose ((string)tag.Value, track.Comment);
                        break;
                    case CommonTags.TrackNumber:
                        int track_number = (int)tag.Value;
                        track.TrackNumber = track_number == 0 ? track.TrackNumber : track_number;
                        break;
                    case CommonTags.TrackCount:
                        track.TrackCount = (int)tag.Value;
                        break;
                    case CommonTags.BeatsPerMinute:
                        track.Bpm = (int)tag.Value;
                        break;
                    case CommonTags.Duration:
                        if (tag.Value is TimeSpan) {
                            track.Duration = (TimeSpan)tag.Value;
                        } else {
                            track.Duration = new TimeSpan ((uint)tag.Value * TimeSpan.TicksPerMillisecond);
                        }
                        break;
                    case CommonTags.MoreInfoUri:
                        track.MoreInfoUri = (SafeUri)tag.Value;
                        break;
                    /* No year tag in GST it seems 
                    case CommonTags.Year:
                        track.Year = (uint)tag.Value;
                        break;*/
                    case CommonTags.NominalBitrate:
                        track.BitRate = (int)tag.Value;
                        break;
                    case CommonTags.StreamType:
                        track.MimeType = (string)tag.Value;
                        break;
                    case CommonTags.VideoCodec:
                        if (tag.Value != null) {
                            track.MediaAttributes |= TrackMediaAttributes.VideoStream;
                        }
                        break;
                    /*case CommonTags.AlbumCoverId:
                        foreach(string ext in TrackInfo.CoverExtensions) {
                            string path = Paths.GetCoverArtPath((string) tag.Value, "." + ext);
                            if(System.IO.File.Exists(path)) {
                                track.CoverArtFileName = path;
                                break;
                            }
                        }
                        break;*/
                }
            } catch {
            }
        }
    }
}

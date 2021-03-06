//
// MusicBrainzQueryJob.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Aurélien Mino <aurelien.mino@gmail.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
// Copyright (C) 2010 Aurélien Mino
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
using System.Collections;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Web;
using System.Text.RegularExpressions;

using MusicBrainz;

using Hyena;
using Banshee.Base;
using Banshee.Metadata;
using Banshee.Kernel;
using Banshee.Collection;
using Banshee.Streaming;
using Banshee.Networking;
using Banshee.Collection.Database;

namespace Banshee.Metadata.MusicBrainz
{
    public class MusicBrainzQueryJob : MetadataServiceJob
    {
        private static string AmazonUriFormat = "http://images.amazon.com/images/P/{0}.01._SCLZZZZZZZ_.jpg";

        class CoverArtSite
        {
             public Regex Regex;
             public string ImgURI;

             public CoverArtSite (Regex regex, string img_URI) {
                this.Regex = regex;
                this.ImgURI = img_URI;
             }
        }

        private static CoverArtSite [] CoverArtSites = new CoverArtSite [] {
            // CDBaby
            new CoverArtSite (
                new Regex (@"http://(?:www\.)?cdbaby.com/cd/(\w)(\w)(\w*)"),
                "http://cdbaby.name/{0}/{1}/{0}{1}{2}.jpg"
            ),
            // Jamendo
            new CoverArtSite (
                new Regex (@"http:\/\/(?:www\.)?jamendo.com\/(?:[a-z]+\/)?album\/([0-9]+)"),
                "http://www.jamendo.com/get/album/id/album/artworkurl/redirect/{0}/?artwork_size=0"
            )
        };

        public MusicBrainzQueryJob (IBasicTrackInfo track)
        {
            Track = track;
            MusicBrainzService.UserAgent = Banshee.Web.Browser.UserAgent;
        }

        public override void Run ()
        {
            Lookup ();
        }

        public bool Lookup ()
        {
            if (!OnlineMetadataServiceJob.TrackConditionsMet (Track)) {
                return false;
            }

            string artwork_id = Track.ArtworkId;

            if (artwork_id == null) {
                return false;
            } else if (CoverArtSpec.CoverExists (artwork_id)) {
                return false;
            } else if (!InternetConnected) {
                return false;
            }

            DatabaseTrackInfo dbtrack;
            dbtrack = Track as DatabaseTrackInfo;

            Release release;

            // If we have the MBID of the album, we can do a direct MusicBrainz lookup
            if (dbtrack != null && dbtrack.AlbumMusicBrainzId != null) {

                release = Release.Get (dbtrack.AlbumMusicBrainzId);
                if (!String.IsNullOrEmpty (release.GetAsin ()) && SaveCover (String.Format (AmazonUriFormat, release.GetAsin ()))) {
                    return true;
                }

            // Otherwise we do a MusicBrainz search
            } else {
                ReleaseQueryParameters parameters = new ReleaseQueryParameters ();
                parameters.Title = Track.AlbumTitle;
                parameters.Artist = Track.AlbumArtist;
                if (dbtrack != null) {
                    parameters.TrackCount = dbtrack.TrackCount;
                }

                Query<Release> query = Release.Query (parameters);
                release = query.PerfectMatch ();

                foreach (Release r in query.Best ()) {
                    if (!String.IsNullOrEmpty (r.GetAsin ()) && SaveCover (String.Format (AmazonUriFormat, r.GetAsin ()))) {
                        return true;
                    }
                }
            }

            if (release == null) {
                return false;
            }

            // No success with ASIN, let's try with other linked URLs
            ReadOnlyCollection<UrlRelation> relations = release.GetUrlRelations ();
            foreach (UrlRelation relation in relations) {

                foreach (CoverArtSite site in CoverArtSites) {

                   Match m = site.Regex.Match (relation.Target.AbsoluteUri);
                   if (m.Success) {
                        string [] parameters = new string [m.Groups.Count];
                        for (int i = 1; i < m.Groups.Count; i++) {
                            parameters[i-1] = m.Groups[i].Value;
                        }

                        String uri = String.Format (site.ImgURI, parameters);
                        if (SaveCover (uri)) {
                             return true;
                        }
                   }
                }

                if (relation.Type == "CoverArtLink" && SaveCover (relation.Target.AbsoluteUri)) {
                   return true;
                }
            }

            return false;
        }

        private bool SaveCover (string uri) {

            string artwork_id = Track.ArtworkId;

            if (SaveHttpStreamCover (new Uri (uri), artwork_id, null)) {
                Log.Debug ("Downloaded cover art", artwork_id);
                StreamTag tag = new StreamTag ();
                tag.Name = CommonTags.AlbumCoverId;
                tag.Value = artwork_id;

                AddTag (tag);
                return true;
            }
            return false;
         }

    }
}

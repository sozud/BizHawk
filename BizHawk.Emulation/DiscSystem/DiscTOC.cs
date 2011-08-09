﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;

namespace BizHawk.DiscSystem
{

	public class DiscTOC
	{
		public class Session
		{
			public int num;
			public List<Track> Tracks = new List<Track>();

			//the length of the session (should be the sum of all track lengths)
			public int length_lba;
			public Cue.CueTimestamp FriendlyLength { get { return new Cue.CueTimestamp(length_lba); } }
		}

		public class Track
		{
			public ETrackType TrackType;
			public int num;
			public List<Index> Indexes = new List<Index>();

			/// <summary>
			/// a track logically starts at index 1. 
			/// so this is the length from this index 1 to the next index 1 (or the end of the disc)
			/// the time before track 1 index 1 is the lead-in and isn't accounted for in any track...
			/// </summary>
			public int length_lba;
			public Cue.CueTimestamp FriendlyLength { get { return new Cue.CueTimestamp(length_lba); } }
		}

		public class Index
		{
			public int num;
			public int lba;

			//the length of the section
			//HEY! This is commented out because it is a bad idea.
			//The length of a section is almost useless, and if you want it, you are probably making an error.
			//public int length_lba;
			//public Cue.CueTimestamp FriendlyLength { get { return new Cue.CueTimestamp(length_lba); } }
		}

		public string GenerateCUE_OneBin(CueBinPrefs prefs)
		{
			if (prefs.OneBlobPerTrack) throw new InvalidOperationException("OneBinPerTrack passed to GenerateCUE_OneBin");

			//this generates a single-file cue!!!!!!! dont expect it to generate bin-per-track!
			StringBuilder sb = new StringBuilder();

			bool leadin = true;
			foreach (var session in Sessions)
			{
				if (!prefs.SingleSession)
				{
					//dont want to screw around with sessions for now
					if (prefs.AnnotateCue) sb.AppendFormat("SESSION {0:D2} (length={1})\n", session.num, session.length_lba);
					else sb.AppendFormat("SESSION {0:D2}\n", session.num);
				}

				foreach (var track in session.Tracks)
				{
					ETrackType trackType = track.TrackType;
					//mutate track type according to our principle of canonicalization 
					if (trackType == ETrackType.Mode1_2048 && prefs.DumpECM)
						trackType = ETrackType.Mode1_2352;

					if (prefs.AnnotateCue) sb.AppendFormat("  TRACK {0:D2} {1} (length={2})\n", track.num, Cue.TrackTypeStringForTrackType(trackType), track.length_lba);
					else sb.AppendFormat("  TRACK {0:D2} {1}\n", track.num, Cue.TrackTypeStringForTrackType(trackType));
					foreach (var index in track.Indexes)
					{
						if (prefs.OmitRedundantIndex0 && index.num == 0 && index.lba == track.Indexes[1].lba)
						{
							//dont emit index 0 when it is the same as index 1. it confuses daemon tools.
							//(make this an option?)
						}
						else if (leadin)
						{
							//don't generate the first index, it is illogical
						}
						else
						{
							//subtract leadin. CUE format seems to always imply this exact amount.
							//however, physical discs could possibly have a slightly longer lead-in.
							//this could be done with a pregap on track 1 perhaps.. 
							//would we handle it here correctly? i think so
							int lba = index.lba - 150;
							sb.AppendFormat("    INDEX {0:D2} {1}\n", index.num, new Cue.CueTimestamp(lba).Value);
						}

						leadin = false;
					}
				}
			}

			return sb.ToString();
		}

		public List<Session> Sessions = new List<Session>();
		public int length_lba;
		public Cue.CueTimestamp FriendlyLength { get { return new Cue.CueTimestamp(length_lba); } }

		public long BinarySize
		{
			get { return length_lba*2352; }
		}

		public void AnalyzeLengthsFromIndexLengths()
		{
			//this is a little more complex than it looks, because the length of a thing is not determined by summing it
			//but rather by the difference in lbas between start and end
			length_lba = 0;
			foreach (var session in Sessions)
			{
				var firstTrack = session.Tracks[0];
				var lastTrack = session.Tracks[session.Tracks.Count - 1];
				session.length_lba = lastTrack.Indexes[0].lba + lastTrack.length_lba - firstTrack.Indexes[0].lba;
				length_lba += session.length_lba;
			}
		}
	}

}
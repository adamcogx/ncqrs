using System;

namespace MyNotes.Events
{
	[Serializable]
	public class NoteAdded
	{
		public Guid Id
		{
			get; set;
		}

		public string Text
		{
			get; set;
		}

		public DateTime CreationDate
		{
			get; set;
		}
	}
}
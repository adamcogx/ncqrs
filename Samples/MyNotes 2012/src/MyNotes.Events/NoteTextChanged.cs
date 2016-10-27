using System;

namespace MyNotes.Events
{
	[Serializable]
	public class NoteTextChanged
	{
		public string Text
		{
			get; set;
		}
	}
}
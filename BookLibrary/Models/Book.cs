using Microsoft.EntityFrameworkCore;

namespace BookLibrary.Api.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int AuthorId { get; set; }

        //public Author? Author { get; set; }   // navigation property
    }

    public class WorksJsonParsedInfo
    {
        //public string[]? Authors { get; set; } // author_role typ
        //public string[]? TranslatedTitles { get; set; } //translated_string type
        public string[]? Subjects { get; set; }
        public string[]? SubjectTimes { get; set; }
        public string[]? SubjectPeople { get; set; }

        // "description": {"type": "/type/text", "value": "In order to protect the people and the world she loves from the future she sees in increasingly horrific visions, Hai is forced to throw away her own happiness and ascend the serpiente throne."}
        public Works_Text? Description { get; set; }
        public string[]? DeweyNumber { get; set; }
        public string? FirstSentence { get; set; } = string.Empty;
        // public string[]? OriginalLanguages { get; set; } // custom class language
    }

    public class Works
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; } = string.Empty;
        public DateTime? LastModified{ get; set; }
        public string? OLKey { get; set; } = string.Empty;

        // Needs to be parsed, might be better to put in logic that populates these fields instead?
        public WorksJsonParsedInfo? RawJson { get; set; }

        //public WorksJsonParsedInfo? WorksJsonInfo { get; set; }
        //public string[]? Authors { get; set; }
        //public string[]? TranslatedTitles { get; set; }
        //public string[]? Subjects { get; set; }
        //public string[]? SubjectTimes { get; set; }
        //public string[]? SubjectPeople { get; set; }
        //public string[]? Description { get; set; }
        //public string[]? DeweyNumber { get; set; }
        //public string? FirstSentence { get; set; } = string.Empty;
        //public string[]? OtherLanguages { get; set; } // custom class?
    }

    public record WorkSummaryDTO(int Id, string Title, string Subtitle, string RawJson, DateTime? LastModified, string OLKey);

    public class Works_Text
    {
        public string Type { get; set; }
        public string Value { get; set; }
    }
}

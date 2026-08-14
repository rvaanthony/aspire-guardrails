using System.ComponentModel.DataAnnotations;

namespace Template.Client.Models;

public class ContactFormModel
{
    public const string TopicGeneral = "general";
    public const string TopicSupport = "support";
    public const string TopicFeedback = "feedback";

    [Required(ErrorMessage = "Please choose what this is about.")]
    public string Topic { get; set; } = TopicGeneral;

    [Required(ErrorMessage = "Please tell us your name.")]
    [StringLength(120)]
    [Display(Name = "Your name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please add an email so we can reply.")]
    [EmailAddress(ErrorMessage = "That email address doesn't look right.")]
    [StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Organization (optional)")]
    public string? Organization { get; set; }

    [Required(ErrorMessage = "Please share a few words about what you need.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Please share at least a sentence.")]
    [Display(Name = "How can we help?")]
    public string Message { get; set; } = string.Empty;

    // Honeypot — humans never see or fill this field; bots do.
    public string? Website { get; set; }

    public static bool IsKnownTopic(string? topic) =>
        topic is TopicGeneral or TopicSupport or TopicFeedback;
}

using StudyData.Core.Coding;

namespace StudyData.Coding;

public interface ICodingEngine
{
    /// <summary>Version string of a loaded dictionary, for example "MedDRA 27.1".</summary>
    string DictionaryVersion(string dictionary);

    /// <summary>Candidates for a verbatim term, best first. Score 100 is an exact lowest level term / drug name match.</summary>
    IReadOnlyList<CodingCandidate> Search(string dictionary, string verbatim, int maxCandidates);
}

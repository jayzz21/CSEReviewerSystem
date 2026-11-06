namespace CseReviewer;

public static class Samples
{
    public static List<Question> Create(GenerateRequest r) => QuestionBank.Create(r);
}

namespace CseReviewer;
public static class Samples {
    public static List<Question> Create(GenerateRequest r) {
        if (r.Category == "Numerical Ability") return Enumerable.Range(1, r.Count).Select(i => {
            var price = (i + 3) * 100; var discount = price / 5; var answer = price - discount;
            var values = new[] { answer, price - 20, discount, price + discount };
            var offset = i % 4; var options = Enumerable.Range(0, 4).Select(j => $"₱{values[(j + offset) % 4]:N0}").ToArray();
            return new Question($"An office chair costs ₱{price:N0}. A supplier offers a 20% discount. What is the sale price?", options, (4 - offset) % 4,
                "Subtract the discount from the original price. The discount amount alone is not the sale price.", [$"Convert 20% to a decimal: 20 ÷ 100 = 0.20.", $"Find the discount: ₱{price} × 0.20 = ₱{discount}.", $"Subtract: ₱{price} − ₱{discount} = ₱{answer}."]);
        }).ToList();
        Question Q(string p, string[] o, int a, string e) => new(p, o, a, e, []);
        return r.Category switch {
            "Verbal Ability" => [
                Q("Choose the synonym of diligent.", ["Careless", "Hardworking", "Reluctant", "Hasty"], 1, "Diligent means showing steady care and effort. Hardworking is the closest synonym."),
                Q("Each of the applicants ___ a valid ID.", ["have", "having", "has", "are having"], 2, "Each is singular, so it takes the singular verb has."),
                Q("Ano ang kasalungat ng salitang matipid?", ["Magastos", "Masinop", "Maingat", "Tahimik"], 0, "Ang matipid ay maingat sa paggastos; ang magastos ay kabaligtaran nito."),
                Q("Choose the correctly written sentence.", ["They was early.", "She don't agree.", "We is ready.", "He has submitted the form."], 3, "He takes has. The other sentences have subject–verb agreement errors."),
                Q("The office closes at 5 p.m. Applications must be received before closing. Which follows?", ["Applications are accepted all night.", "An application received at 6 p.m. misses that day's cutoff.", "The office opens at noon.", "All applications are approved."], 1, "6 p.m. is after the stated 5 p.m. cutoff. Nothing is said about opening times or approval.")],
            "Analytical Ability" => [
                Q("All inspectors are employees. Ana is an inspector. Which must be true?", ["Ana is an employee.", "All employees are inspectors.", "Ana manages all employees.", "No inspectors work."], 0, "Ana belongs to a group fully contained within employees. The reverse relationship does not follow."),
                Q("Book is to reading as fork is to ___.", ["Writing", "Eating", "Walking", "Listening"], 1, "The relationship is an object and its primary use."),
                Q("Liza is ahead of Ben. Ben is ahead of Carlo. Who is last?", ["Liza", "Ben", "Carlo", "Cannot be determined"], 2, "The order is Liza, Ben, Carlo, placing Carlo last."),
                Q("Some clerks are writers. All writers are readers. Which follows?", ["All clerks are readers.", "No clerks read.", "All readers are writers.", "Some clerks are readers."], 3, "The clerks who are writers must also be readers; this does not establish that all clerks read."),
                Q("If a request is approved, a notice is sent. No notice was sent. What follows?", ["It was approved.", "It was not approved.", "Every request is rejected.", "A notice was lost."], 1, "By contraposition, no notice implies no approval under the stated rule.")],
            "Clerical Ability" => [
                Q("Which name comes first alphabetically?", ["Santos", "Reyes", "Cruz", "Aquino"], 3, "Alphabetical order is Aquino, Cruz, Reyes, Santos."),
                Q("Choose the correctly spelled word.", ["Accomodation", "Accommodation", "Acommodation", "Accommmodation"], 1, "Accommodation has two c's and two m's."),
                Q("Which code exactly matches AB-4072-X?", ["AB-4702-X", "AB-4072-Y", "AB-4072-X", "BA-4072-X"], 2, "Only AB-4072-X preserves every letter and digit in the original order."),
                Q("Which file comes immediately after Cruz, Ana alphabetically?", ["Cruz, Ben", "Cruz, Ada", "Castro, Ben", "Cruz, Abel"], 0, "Within Cruz, the given-name order is Abel, Ada, Ana, Ben."),
                Q("Choose the correctly spelled word.", ["Recieve", "Receive", "Receeve", "Receve"], 1, "Receive is spelled r-e-c-e-i-v-e.")],
            _ => [
                Q("Which action best demonstrates accountability in public service?", ["Concealing errors", "Explaining decisions and correcting mistakes", "Favoring friends", "Ignoring records"], 1, "Accountability means accepting responsibility and making decisions open to scrutiny."),
                Q("Which practice best protects personal information?", ["Posting all records online", "Sharing passwords", "Limiting access to authorized personnel", "Leaving files in public"], 2, "Limiting access reduces unauthorized disclosure; the other options expose information."),
                Q("Which action reduces waste at its source?", ["Using reusable containers", "Burning plastic", "Dumping rubbish", "Buying more disposable cups"], 0, "Reuse prevents disposable waste from being created."),
                Q("Which is an example of a conflict of interest?", ["Serving clients in order", "Declaring a personal interest", "Following published criteria", "Choosing a relative's business for personal benefit"], 3, "Personal benefit can compromise impartial public decisions."),
                Q("Which practice supports peaceful conflict resolution?", ["Threats", "Dialogue and mediation", "Spreading rumors", "Retaliation"], 1, "Dialogue and mediation address disagreements through communication rather than escalation.")]
        };
    }
}

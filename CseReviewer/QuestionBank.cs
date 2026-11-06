using System.Security.Cryptography;

namespace CseReviewer;

/// <summary>
/// Offline question bank sourced from the 2027 Civil Service Exam Reviewer (3 sets × 180 items).
/// Every run randomizes:
///  1. The selected questions for the category (5, 10, 20, or 30 items)
///  2. The placement of answer choices (A, B, C, D) so the correct answer is randomized
/// </summary>
public static class QuestionBank
{
    private record PoolItem(string Prompt, string Correct, string[] Distractors, string Explanation, string[] Steps);

    private static PoolItem Item(string p, string c, string[] d, string exp, string[]? steps = null) =>
        new(p, c, d, exp, steps ?? []);

    // ════════════════════════════════════════════════════════════════════════
    //  VERBAL ABILITY (40 items: Synonyms, Antonyms, Grammar, Comprehension)
    // ════════════════════════════════════════════════════════════════════════
    private static readonly PoolItem[] VerbalPool =
    [
        // Synonyms
        Item("Choose the word that means the same as <u>diligent</u>.", "Hardworking", ["Lazy", "Careless", "Forgetful"], "Diligent means working carefully and persistently; hardworking is the closest synonym."),
        Item("Choose the word that means the same as <u>abundant</u>.", "Plentiful", ["Scarce", "Rare", "Limited"], "Abundant means existing in large quantity; plentiful is the closest synonym."),
        Item("Choose the word that means the same as <u>cautious</u>.", "Careful", ["Reckless", "Rash", "Careless"], "Cautious means careful to avoid danger or mistakes."),
        Item("Choose the word that means the same as <u>brave</u>.", "Courageous", ["Fearful", "Timid", "Cowardly"], "Brave means showing courage; courageous is the synonym."),
        Item("Choose the word that means the same as <u>brief</u>.", "Short", ["Lengthy", "Endless", "Extended"], "Brief means short in duration or length."),
        Item("Choose the word that means the same as <u>enormous</u>.", "Huge", ["Tiny", "Small", "Miniature"], "Enormous means very large; huge is the synonym."),
        Item("Choose the word that means the same as <u>honest</u>.", "Truthful", ["Deceitful", "Lying", "False"], "Honest means truthful and sincere."),
        Item("Choose the word that means the same as <u>intelligent</u>.", "Smart", ["Foolish", "Dull", "Slow"], "Intelligent means having good reasoning ability; smart is the synonym."),
        Item("Choose the word that means the same as <u>rapid</u>.", "Fast", ["Slow", "Sluggish", "Gradual"], "Rapid means very quick; fast is the synonym."),
        Item("Choose the word that means the same as <u>vacant</u>.", "Empty", ["Occupied", "Full", "Packed"], "Vacant means empty or not occupied."),
        Item("Choose the word that means the same as <u>purchase</u>.", "Buy", ["Sell", "Trade", "Donate"], "To purchase means to buy."),
        Item("Choose the word that means the same as <u>assist</u>.", "Help", ["Hinder", "Block", "Ignore"], "To assist means to help."),

        // Antonyms
        Item("Choose the word that is opposite in meaning to <u>abundant</u>.", "Scarce", ["Plentiful", "Ample", "Numerous"], "Abundant means plentiful; its opposite is scarce."),
        Item("Choose the word that is opposite in meaning to <u>diligent</u>.", "Lazy", ["Hardworking", "Industrious", "Active"], "Diligent means hardworking; its opposite is lazy."),
        Item("Choose the word that is opposite in meaning to <u>brave</u>.", "Cowardly", ["Courageous", "Fearless", "Bold"], "Brave means courageous; its opposite is cowardly."),
        Item("Choose the word that is opposite in meaning to <u>brief</u>.", "Lengthy", ["Short", "Concise", "Quick"], "Brief means short; its opposite is lengthy."),
        Item("Choose the word that is opposite in meaning to <u>honest</u>.", "Deceitful", ["Truthful", "Sincere", "Fair"], "Honest means truthful; its opposite is deceitful."),
        Item("Choose the word that is opposite in meaning to <u>rapid</u>.", "Slow", ["Fast", "Quick", "Swift"], "Rapid means fast; its opposite is slow."),
        Item("Choose the word that is opposite in meaning to <u>vacant</u>.", "Occupied", ["Empty", "Unused", "Available"], "Vacant means empty; its opposite is occupied."),
        Item("Choose the word that is opposite in meaning to <u>purchase</u>.", "Sell", ["Buy", "Acquire", "Obtain"], "Purchase means buy; its opposite is sell."),
        Item("Choose the word that is opposite in meaning to <u>assist</u>.", "Hinder", ["Help", "Aid", "Support"], "Assist means help; its opposite is hinder."),
        Item("Choose the word that is opposite in meaning to <u>cautious</u>.", "Reckless", ["Careful", "Prudent", "Wary"], "Cautious means careful; its opposite is reckless."),
        Item("Choose the word that is opposite in meaning to <u>enormous</u>.", "Tiny", ["Huge", "Large", "Gigantic"], "Enormous means huge; its opposite is tiny."),
        Item("Choose the word that is opposite in meaning to <u>intelligent</u>.", "Foolish", ["Smart", "Bright", "Clever"], "Intelligent means smart; its opposite is foolish."),

        // Grammar & Sentence Structure
        Item("Select the grammatically correct sentence (Subject–Verb Agreement: \"Each of the employees\"):", "Each of the employees has submitted their report.", ["Each of the employees have submitted their report.", "Each of the employees have submitted his report.", "Each of the employee has submitted their report."], "Each is singular, so it takes the singular verb has. The singular they/their is acceptable and inclusive."),
        Item("Select the grammatically correct sentence (Subject–Verb Agreement: \"Neither of the boys\"):", "Neither of the boys has finished the assignment.", ["Neither of the boys have finished the assignment.", "Neither of the boy has finished the assignment.", "Neither of the boys has finish the assignment."], "Neither is singular, so it takes has."),
        Item("Select the grammatically correct sentence (Collective Nouns: \"The team\"):", "The team is preparing its final report.", ["The team are preparing its final report.", "The team is preparing their final report.", "The team are preparing their final report."], "Team is treated as a singular collective noun here, so it takes is and its."),
        Item("Select the grammatically correct sentence (Pronoun Case: \"She and I\"):", "She and I are going to the seminar.", ["She and me are going to the seminar.", "Her and I are going to the seminar.", "She and I is going to the seminar."], "Use the subject pronoun I, and the plural verb are."),
        Item("Select the grammatically correct sentence (Count Expressions: \"The number of applicants\"):", "The number of applicants is increasing.", ["The number of applicants are increasing.", "A number of applicants is increasing.", "The number of applicant are increasing."], "The number is singular and takes is."),
        Item("Select the grammatically correct sentence (Count Expressions: \"A number of applicants\"):", "A number of applicants are waiting outside.", ["A number of applicants is waiting outside.", "The number of applicants are waiting outside.", "A number of applicant are waiting outside."], "A number of means several, so it takes a plural verb."),
        Item("Select the grammatically correct sentence (Comparative Pronoun: \"faster than\"):", "He runs faster than I do.", ["He runs faster than me.", "He runs faster than I.", "He run faster than I do."], "Than I do is the complete and formal comparison."),
        Item("Select the grammatically correct sentence (Singular Noun: \"The news\"):", "The news is surprising.", ["The news are surprising.", "The news is surprise.", "The news were surprising."], "News is treated as singular."),
        Item("Select the grammatically correct sentence (Academic Subjects: \"Mathematics\"):", "Mathematics is difficult for some students.", ["Mathematics are difficult for some students.", "Mathematics is difficult for some student.", "Mathematics were difficult for some students."], "Mathematics is a singular subject."),
        Item("Select the grammatically correct sentence (Indefinite Pronouns: \"Every student\"):", "Every student has submitted the form.", ["Every student have submitted the form.", "Every students has submitted the form.", "Every student has submit the form."], "Every is singular and takes has."),
        Item("Select the grammatically correct sentence (Prepositional Modifiers: \"The books on the table\"):", "The books on the table are mine.", ["The books on the table is mine.", "The book on the table are mine.", "The books on the table was mine."], "Books is plural, so it takes are."),
        Item("Select the grammatically correct sentence (Correlative Conjunctions: \"Neither the teacher nor the students\"):", "Neither the teacher nor the students are absent.", ["Neither the teacher nor the students is absent.", "Neither the teacher or the students are absent.", "Neither the teacher nor the student are absent."], "With neither...nor, the verb agrees with the nearer subject, students, so are is correct."),

        // Filipino & Comprehension
        Item("Ano ang kasalungat ng salitang matipid?", "Magastos", ["Masinop", "Maingat", "Tahimik"], "Ang matipid ay maingat sa paggastos; ang magastos ay kabaligtaran nito."),
        Item("Each of the applicants ___ a valid ID.", "has", ["have", "having", "are having"], "Each is singular, so it takes the singular verb has."),
        Item("Choose the correctly written sentence.", "He has submitted the form.", ["They was early.", "She don't agree.", "We is ready."], "He takes has. The other sentences have subject–verb agreement errors."),
        Item("The office closes at 5 p.m. Applications must be received before closing. Which follows?", "An application received at 6 p.m. misses that day's cutoff.", ["Applications are accepted all night.", "The office opens at noon.", "All applications are approved."], "6 p.m. is after the stated 5 p.m. cutoff. Nothing is said about opening times or approval.")
    ];

    // ════════════════════════════════════════════════════════════════════════
    //  ANALYTICAL ABILITY (40+ items: Analogies, Assumptions, Syllogisms, Odd-one-out)
    // ════════════════════════════════════════════════════════════════════════
    private static readonly PoolItem[] AnalyticalStaticPool =
    [
        // Analogies
        Item("Moby-Dick is to Herman Melville as The Old Man and the Sea is to ____.", "Ernest Hemingway", ["Confucius", "Antoine de Saint-Exupery", "Mahatma Gandhi"], "The analogy pairs an author with a famous work. Hemingway wrote The Old Man and the Sea."),
        Item("Noli Me Tangere is to Jose Rizal as The Little Prince is to ____.", "Antoine de Saint-Exupery", ["William Shakespeare", "Jane Austen", "F. Scott Fitzgerald"], "The analogy pairs a work with its author. Antoine de Saint-Exupery wrote The Little Prince."),
        Item("Romeo and Juliet is to William Shakespeare as Pride and Prejudice is to ____.", "Jane Austen", ["Ernest Hemingway", "J.K. Rowling", "Jose Rizal"], "Pride and Prejudice was written by Jane Austen."),
        Item("The Great Gatsby is to F. Scott Fitzgerald as Harry Potter is to ____.", "J.K. Rowling", ["Jane Austen", "Herman Melville", "Antoine de Saint-Exupery"], "Harry Potter was written by J.K. Rowling."),
        Item("The Old Man and the Sea is to Ernest Hemingway as Moby-Dick is to ____.", "Herman Melville", ["William Shakespeare", "Jose Rizal", "Jane Austen"], "Moby-Dick was written by Herman Melville."),
        Item("Pride and Prejudice is to Jane Austen as Noli Me Tangere is to ____.", "Jose Rizal", ["J.K. Rowling", "Ernest Hemingway", "F. Scott Fitzgerald"], "Noli Me Tangere was written by Jose Rizal."),
        Item("Book is to reading as fork is to ____.", "Eating", ["Writing", "Walking", "Listening"], "The relationship is an object and its primary use."),

        // Assumptions
        Item("John knows how to play the piano, so he might have taken a piano class. Which is an assumption behind this statement?", "People who know how to play the piano usually learned through lessons or classes.", ["John owns a piano.", "Piano classes are the only way to learn piano.", "John wants to become a professional pianist."], "The statement assumes that having the skill implies some form of instruction."),
        Item("The company will hire more staff because sales are increasing. Which is an assumption?", "Increasing sales will require more staff to handle the work.", ["The company is losing money.", "All employees are unhappy.", "The company will close next year."], "The argument assumes that higher sales create a need for more personnel."),
        Item("Maria brought an umbrella, so it will probably rain today. Which is an assumption?", "People usually bring umbrellas when rain is likely.", ["Maria likes the color blue.", "It rained yesterday.", "Umbrellas are expensive."], "The statement assumes a link between bringing an umbrella and expecting rain."),
        Item("Pedro studied hard, so he will pass the exam. Which is an assumption?", "Studying hard increases the chance of passing the exam.", ["Pedro is always late.", "The exam is very easy.", "Pedro did not attend class."], "The conclusion depends on the assumption that studying hard helps one pass."),
        Item("The office installed new computers to improve productivity. Which is an assumption?", "New computers will help employees work more efficiently.", ["The old computers were free.", "Employees prefer paper files.", "The office will move to a new building."], "The plan assumes that better equipment leads to better productivity."),
        Item("Ana will join the government because she wants to serve the public. Which is an assumption?", "Government service is a way to serve the public.", ["Government pay is always higher.", "Ana dislikes private companies.", "Ana has no other skills."], "The argument assumes that government work involves public service."),

        // Syllogisms & Logic
        Item("All employees are punctual. Maria is an employee. Therefore, ____.", "Maria is punctual.", ["Maria is late.", "Maria is not an employee.", "All punctual people are employees."], "From the two premises, Maria must be punctual."),
        Item("All citizens have rights. Ana is a citizen. Therefore, ____.", "Ana has rights.", ["Ana has no rights.", "All rights belong only to Ana.", "Ana is not a citizen."], "Ana falls under the category of citizens, so she has rights."),
        Item("No one may enter without an ID. Pedro entered. Therefore, ____.", "Pedro had an ID.", ["Pedro forgot his ID.", "Pedro did not enter.", "No one entered."], "Since entry requires an ID, Pedro must have had one."),
        Item("All government employees are public servants. Juan is a government employee. Therefore, ____.", "Juan is a public servant.", ["Juan is not a public servant.", "All public servants are government employees.", "Juan is unemployed."], "Juan belongs to the category of government employees, so he is a public servant."),
        Item("Some books are novels. All novels are fiction. Therefore, ____.", "Some books are fiction.", ["All books are fiction.", "No books are fiction.", "Some fiction are not books."], "The overlap between books and novels, and novels and fiction, means some books are fiction."),
        Item("All doctors studied medicine. Carlo is a doctor. Therefore, ____.", "Carlo studied medicine.", ["Carlo studied law.", "All who studied medicine are doctors.", "Carlo is not a doctor."], "Carlo belongs to the category of doctors, so he studied medicine."),
        Item("All inspectors are employees. Ana is an inspector. Which must be true?", "Ana is an employee.", ["All employees are inspectors.", "Ana manages all employees.", "No inspectors work."], "Ana belongs to a group fully contained within employees. The reverse relationship does not follow."),
        Item("Liza is ahead of Ben. Ben is ahead of Carlo. Who is last?", "Carlo", ["Liza", "Ben", "Cannot be determined"], "The order is Liza, Ben, Carlo, placing Carlo last."),
        Item("Some clerks are writers. All writers are readers. Which follows?", "Some clerks are readers.", ["All clerks are readers.", "No clerks read.", "All readers are writers."], "The clerks who are writers must also be readers; this does not establish that all clerks read."),
        Item("If a request is approved, a notice is sent. No notice was sent. What follows?", "It was not approved.", ["It was approved.", "Every request is rejected.", "A notice was lost."], "By contraposition, no notice implies no approval under the stated rule."),

        // Odd Word Out
        Item("Which word does not belong among these foods? Apple, Banana, Carrot, Mango", "Carrot", ["Apple", "Banana", "Mango"], "Carrot is a vegetable; the others are fruits."),
        Item("Which word does not belong among these household items? Chair, Table, Laptop, Sofa", "Laptop", ["Chair", "Table", "Sofa"], "Laptop is electronic equipment; the others are furniture."),
        Item("Which word does not belong among these visual attributes? Red, Blue, Green, Circle", "Circle", ["Red", "Blue", "Green"], "Circle is a shape; the others are colors."),
        Item("Which word does not belong among these vehicles? Jeepney, Bus, Car, Bicycle", "Bicycle", ["Jeepney", "Bus", "Car"], "Bicycle has two wheels and is non-motorized; the others are motor vehicles."),
        Item("Which word does not belong among these places? Manila, Cebu, Davao, Philippines", "Philippines", ["Manila", "Cebu", "Davao"], "Philippines is a country; the others are cities."),
        Item("Which word does not belong among these roles? Teacher, Doctor, Engineer, Hospital", "Hospital", ["Teacher", "Doctor", "Engineer"], "Hospital is a facility; the others are professions.")
    ];

    // ════════════════════════════════════════════════════════════════════════
    //  CLERICAL ABILITY (36 items: Filing, Office Practices, Documents, Spelling, Coding)
    // ════════════════════════════════════════════════════════════════════════
    private static readonly PoolItem[] ClericalPool =
    [
        // Alphabetical Filing
        Item("Which name should be filed first (Cruz group)?", "Cruz, Juan", ["Cruz, Juan Ramon", "Cruz, Juancho", "Cruz, Juanita"], "Alphabetical filing compares letter by letter; Cruz, Juan comes before the longer names beginning with Juan."),
        Item("Which name should be filed first (Dela Cruz group)?", "Dela Cruz, Ana", ["Dela Cruz, Antonio", "Dela Cruz, Andrea", "Dela Cruz, Angela"], "Ana comes before Andrea, Angela, and Antonio alphabetically."),
        Item("Which name should be filed first (Santos group)?", "Santos, Ben", ["Santos, Benjamin", "Santos, Bernadette", "Santos, Bianca"], "Ben comes before Benjamin, Bernadette, and Bianca."),
        Item("Which name should be filed first (Reyes group)?", "Reyes, Carlo", ["Reyes, Carlos", "Reyes, Carmela", "Reyes, Carmen"], "Carlo comes before Carlos, Carmela, and Carmen."),
        Item("Which name should be filed first (Garcia group)?", "Garcia, Liza", ["Garcia, Liza Mae", "Garcia, Lorna", "Garcia, Luz"], "Liza comes before Liza Mae, Lorna, and Luz."),
        Item("Which name should be filed first (Torres group)?", "Torres, Mark", ["Torres, Maria", "Torres, Mario", "Torres, Marlon"], "Mark comes before Maria, Mario, and Marlon."),
        Item("Which name comes first alphabetically in this group?", "Aquino", ["Santos", "Reyes", "Cruz"], "Alphabetical order is Aquino, Cruz, Reyes, Santos."),
        Item("Which file comes immediately after Cruz, Ana alphabetically?", "Cruz, Ben", ["Cruz, Ada", "Castro, Ben", "Cruz, Abel"], "Within Cruz, the given-name order is Abel, Ada, Ana, Ben."),

        // Office Procedures
        Item("Which department is responsible for hiring new personnel?", "Human Resources", ["Accounting", "Logistics", "Maintenance"], "Hiring is a core function of Human Resources."),
        Item("What is the best way to answer a phone call in a company?", "Identify the company and your department, then offer assistance.", ["Say “Hello” and wait for the caller to speak.", "Transfer the call immediately to your supervisor.", "Ask the caller to call back later."], "Professional telephone etiquette requires identifying the company and department before offering help."),
        Item("What should you do first when receiving an official letter for your supervisor?", "Record it in the incoming logbook.", ["Open and discard it.", "Post it on social media.", "Keep it without recording."], "Incoming documents should be logged for tracking and accountability."),
        Item("Which is the proper way to handle a confidential document?", "Keep it secured and disclose it only to authorized persons.", ["Leave it on a desk.", "Share it with all employees.", "Throw it in a regular trash bin."], "Confidential documents must be protected and shared only with authorized personnel."),
        Item("What is the purpose of a memorandum?", "To communicate official information within an organization.", ["To advertise a product.", "To record a court decision.", "To apply for a passport."], "A memorandum is an internal written message in an office."),
        Item("What should you do if you cannot answer a caller’s question?", "Politely refer the caller to the correct person or department.", ["Guess the answer.", "Hang up immediately.", "Tell the caller to stop calling."], "Referrals are proper when you are not the right person to answer."),

        // Document Management
        Item("Which document is used to request something officially?", "Request letter", ["Receipt", "Invoice", "Memorandum"], "A request letter formally asks for an action or item."),
        Item("What should be done with incoming mail?", "Log, sort, and route it to the proper office.", ["Discard it after opening.", "Keep it unopened for a month.", "Give it to any employee."], "Incoming mail must be logged, sorted, and routed properly."),
        Item("Which is a good filing practice?", "File documents promptly and consistently.", ["File documents only when convenient.", "Keep all papers in one unlabeled box.", "Avoid labeling folders."], "Prompt and consistent filing makes retrieval easier."),
        Item("What does “for signature” mean on a routing slip?", "The document is being forwarded for signing.", ["The document is for disposal.", "The document is for photocopying only.", "The document is for public posting."], "The routing slip indicates the document needs a signature."),
        Item("What is the proper way to correct a typographical error in a formal document?", "Use a clean correction or reprint the page.", ["Use correction fluid on every page.", "Leave it uncorrected.", "Cross out the whole paragraph."], "Formal documents should be clean and professional."),

        // Spelling
        Item("Choose the correctly spelled word for lodging/living quarters:", "Accommodation", ["Accomodation", "Acommodation", "Accommmodation"], "Accommodation has two c's and two m's."),
        Item("Choose the correctly spelled word for an organized body of persons:", "Committee", ["Comittee", "Commitee", "Committe"], "Committee has double m, double t, and double e."),
        Item("Choose the correctly spelled word for without doubt / certainly:", "Definitely", ["Definately", "Definitly", "Defenitely"], "Definitely is spelled d-e-f-i-n-i-t-e-l-y."),
        Item("Choose the correctly spelled word for set apart / distinct:", "Separate", ["Seperate", "Separete", "Separrate"], "Separate has “par” in the middle."),
        Item("Choose the correctly spelled word for the political administration of a nation:", "Government", ["Goverment", "Governmant", "Govenment"], "Government includes the “n” before ment."),
        Item("Choose the correctly spelled word for a proof-of-purchase document:", "Receipt", ["Reciept", "Receit", "Recept"], "Receipt is spelled r-e-c-e-i-p-t."),
        Item("Choose the correctly spelled word for to accept or take delivery of:", "Receive", ["Recieve", "Receeve", "Receve"], "Receive is spelled r-e-c-e-i-v-e."),

        // Coding, Number & Formats
        Item("In filing, which comes first: “A. Reyes” or “B. Reyes”?", "A. Reyes", ["B. Reyes", "They are the same.", "Cannot be determined"], "Filing is usually by surname, then by first initial; A comes before B."),
        Item("Which is the correct order for a date written in a formal letter?", "Month Day, Year", ["Day Month Year", "Year Month Day", "Month Year Day"], "A common formal format is Month Day, Year, e.g., June 12, 2027."),
        Item("What does “CC” mean in a memorandum?", "Copy furnished", ["Central control", "Current cost", "Certified correct"], "CC means copy furnished or carbon copy."),
        Item("Which is a proper subject line for a memorandum?", "Subject: Submission of Monthly Report", ["Subject: Hello", "Subject: None", "Subject: Important"], "A subject line should be specific and informative."),
        Item("What is the purpose of an index in a filing system?", "To help locate files quickly.", ["To decorate the folder.", "To replace the files.", "To hide documents."], "An index makes retrieval faster."),
        Item("Which folder label is most appropriate?", "2027 Personnel Files", ["Miscellaneous", "Others", "Stuff"], "Specific labels improve filing and retrieval."),
        Item("Which code exactly matches AB-4072-X?", "AB-4072-X", ["AB-4702-X", "AB-4072-Y", "BA-4072-X"], "Only AB-4072-X preserves every letter and digit in the original order.")
    ];

    // ════════════════════════════════════════════════════════════════════════
    //  GENERAL INFORMATION (42 items: Constitution, RA 6713, Environmental laws, Ethics)
    // ════════════════════════════════════════════════════════════════════════
    private static readonly PoolItem[] GeneralInfoPool =
    [
        Item("Which branch of government is authorized to make, alter, and repeal laws?", "Legislative", ["Executive", "Judicial", "Constitutional"], "Under the 1987 Constitution, the legislative branch makes laws."),
        Item("What is the term of office of the President of the Philippines?", "6 years", ["4 years", "5 years", "3 years"], "The President and Vice-President serve a single six-year term without reelection."),
        Item("What is the main purpose of R.A. 6713?", "To set ethical standards and promote integrity in public service.", ["To promote economic growth.", "To regulate environmental protection.", "To define the powers of the Ombudsman."], "R.A. 6713 is the Code of Conduct and Ethical Standards for Public Officials and Employees."),
        Item("Which branch implements and enforces laws?", "Executive", ["Legislative", "Judicial", "Constitutional"], "The executive branch implements and enforces laws."),
        Item("Which branch interprets laws?", "Judicial", ["Executive", "Legislative", "Constitutional"], "The judiciary interprets laws and settles legal disputes."),
        Item("In what year was the present Constitution of the Philippines ratified?", "1987", ["1973", "1935", "1992"], "The 1987 Constitution is the present fundamental law of the Philippines."),
        Item("What is the term of office of a Senator?", "6 years", ["3 years", "4 years", "5 years"], "Senators serve six-year terms and may be re-elected for no more than two consecutive terms."),
        Item("What is the term of office of a Member of the House of Representatives?", "3 years", ["6 years", "4 years", "5 years"], "Members of the House serve three-year terms."),
        Item("How many Senators are elected nationwide?", "24", ["12", "20", "30"], "The Senate has 24 members elected at large."),
        Item("Which law is known as the Local Government Code?", "R.A. 7160", ["R.A. 6713", "R.A. 3019", "R.A. 9003"], "R.A. 7160 is the Local Government Code of 1991."),
        Item("Which law is the Anti-Graft and Corrupt Practices Act?", "R.A. 3019", ["R.A. 6713", "R.A. 7160", "R.A. 8749"], "R.A. 3019 addresses graft and corrupt practices in government."),
        Item("Which office investigates and prosecutes public officials for corruption?", "Ombudsman", ["COMELEC", "COA", "CSC"], "The Ombudsman investigates and prosecutes corruption cases."),
        Item("What is the Bill of Rights?", "A list of fundamental rights protected by the Constitution.", ["A list of government projects.", "A set of tax rules.", "A schedule of elections."], "The Bill of Rights (Article III) protects individual freedoms and civil liberties."),
        Item("Where does sovereignty reside according to the Constitution?", "The people", ["Congress", "The President", "The Supreme Court"], "Article II Section 1 states: Sovereignty resides in the people and all government authority emanates from them."),
        Item("What is the minimum age for suffrage in the Philippines?", "18", ["16", "20", "21"], "Filipino citizens at least 18 years old who meet residency requirements may vote."),
        Item("What are the official languages of the Philippines?", "Filipino and English", ["Filipino and Spanish", "English and Spanish", "Tagalog and Cebuano"], "The Constitution provides for Filipino and English as official languages for communication and instruction."),
        Item("What is the national anthem of the Philippines?", "Lupang Hinirang", ["Bayan Ko", "Pilipinas Kong Mahal", "Ako ay Pilipino"], "Lupang Hinirang is the national anthem of the Philippines."),
        Item("Which law governs the national flag and other heraldic items?", "R.A. 8491", ["R.A. 6713", "R.A. 9003", "R.A. 7160"], "R.A. 8491 is the Flag and Heraldic Code of the Philippines."),
        Item("Which law provides for solid waste management?", "R.A. 9003", ["R.A. 8749", "R.A. 9275", "R.A. 3019"], "R.A. 9003 is the Ecological Solid Waste Management Act of 2000."),
        Item("Which law is the Clean Air Act?", "R.A. 8749", ["R.A. 9003", "R.A. 9275", "R.A. 7160"], "R.A. 8749 is the Philippine Clean Air Act of 1999."),
        Item("Which law is the Clean Water Act?", "R.A. 9275", ["R.A. 8749", "R.A. 9003", "R.A. 6713"], "R.A. 9275 is the Philippine Clean Water Act of 2004."),
        Item("Which constitutional commission handles elections?", "COMELEC", ["CSC", "COA", "Ombudsman"], "COMELEC administers and enforces election laws."),
        Item("Which constitutional commission audits government funds?", "COA", ["COMELEC", "CSC", "Ombudsman"], "The Commission on Audit (COA) examines and audits government expenditures."),
        Item("Which constitutional commission is the central personnel agency?", "CSC", ["COMELEC", "COA", "Ombudsman"], "The Civil Service Commission is the central personnel agency of the Philippine government."),
        Item("Who initiates impeachment cases in Congress?", "House of Representatives", ["Senate", "Supreme Court", "President"], "The House of Representatives has the exclusive power to initiate impeachment cases."),
        Item("Who tries impeachment cases?", "Senate", ["House of Representatives", "Supreme Court", "COMELEC"], "The Senate has the sole power to try and decide impeachment cases."),
        Item("Which official may be removed through impeachment?", "President", ["Barangay tanod", "Public school teacher", "City traffic enforcer"], "The President, Vice President, SC Justices, and heads of Constitutional Commissions can be removed by impeachment."),
        Item("What are the local government units in the Philippines?", "Provinces, cities, municipalities, and barangays", ["Departments, bureaus, and agencies", "Senate, House, and courts", "CSC, COMELEC, and COA"], "These are the political subdivisions under the 1987 Constitution and the Local Government Code."),
        Item("What is celebrated every June 12 in the Philippines?", "Independence Day", ["Labor Day", "Christmas Day", "Rizal Day"], "June 12 commemorates Philippine Independence from Spain proclaimed in 1898."),
        Item("Who is the head of a barangay?", "Punong Barangay", ["Governor", "Mayor", "President"], "The Punong Barangay (Barangay Captain) heads the barangay government."),
        Item("Which body has the power to declare a law unconstitutional?", "Supreme Court", ["Congress", "President", "COMELEC"], "Judicial review is exercised by the courts, ultimately the Supreme Court."),
        Item("What does “public office is a public trust” mean?", "Public officials must serve the people with accountability and integrity.", ["Public officials own their offices.", "Public office is for personal gain.", "Public officials are exempt from laws."], "Article XI Section 1 establishes that public officers must at all times be accountable to the people."),
        Item("Which is a constitutional right of every Filipino?", "Right to due process", ["Right to avoid all taxes", "Right to own all public property", "Right to ignore the law"], "Due process is guaranteed by Article III, Section 1 of the Bill of Rights."),
        Item("Which agency is responsible for civil service examinations?", "Civil Service Commission", ["COMELEC", "COA", "Ombudsman"], "The CSC administers Career Service Examinations nationwide."),
        Item("What is the passing rate for the Civil Service Exam?", "80.00%", ["70.00%", "75.00%", "85.00%"], "A general rating of at least 80.00% is required to pass the Philippine CSE."),
        Item("Which of the following is a professional subtest in the CSE?", "Analytical Ability", ["Clerical Ability", "Typing Test", "Driving Test"], "Analytical Ability is included in the Professional level examination."),
        Item("Which of the following is a subprofessional subtest in the CSE?", "Clerical Ability", ["Analytical Ability", "Legal Writing", "Advanced Statistics"], "Clerical Ability is included in the Subprofessional level examination."),
        Item("Which action best demonstrates accountability in public service?", "Explaining decisions and correcting mistakes", ["Concealing errors", "Favoring friends", "Ignoring records"], "Accountability means accepting responsibility and making decisions open to scrutiny."),
        Item("Which practice best protects personal information?", "Limiting access to authorized personnel", ["Posting all records online", "Sharing passwords", "Leaving files in public"], "Limiting access reduces unauthorized disclosure; the other options expose confidential information."),
        Item("Which action reduces waste at its source?", "Using reusable containers", ["Burning plastic", "Dumping rubbish", "Buying more disposable cups"], "Reuse prevents disposable waste from being created in the first place."),
        Item("Which is an example of a conflict of interest?", "Choosing a relative's business for personal benefit", ["Serving clients in order", "Declaring a personal interest", "Following published criteria"], "Personal or familial financial benefit can compromise impartial public decisions."),
        Item("Which practice supports peaceful conflict resolution?", "Dialogue and mediation", ["Threats", "Spreading rumors", "Retaliation"], "Dialogue and mediation address disagreements through communication rather than escalation.")
    ];

    // ════════════════════════════════════════════════════════════════════════
    //  NUMERICAL ABILITY (Dynamic Generator matching the 6 problem types in reviewer)
    // ════════════════════════════════════════════════════════════════════════
    private static List<PoolItem> GenerateNumericalItems(int count, Random rng)
    {
        var items = new List<PoolItem>();
        int seed = 0;

        while (items.Count < count)
        {
            int type = seed % 7;
            seed++;

            PoolItem item = type switch
            {
                // Type 0: Geometric sequence
                0 => CreateSequenceQuestion(rng),
                // Type 1: Order of operations (PEMDAS)
                1 => CreatePemdasQuestion(rng),
                // Type 2: Harvest / addition word problem
                2 => CreateAdditionWordProblem(rng),
                // Type 3: Percentage discount
                3 => CreateDiscountQuestion(rng),
                // Type 4: Average calculation
                4 => CreateAverageQuestion(rng),
                // Type 5: Unit price proportion
                5 => CreateProportionQuestion(rng),
                // Type 6: Office chair 20% discount (from original sample)
                _ => CreateChairDiscountQuestion(rng)
            };

            // Avoid duplicate prompts in the batch
            if (!items.Any(existing => existing.Prompt == item.Prompt))
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static PoolItem CreateSequenceQuestion(Random rng)
    {
        int ratio = rng.Next(2, 4); // 2 or 3
        int start = rng.Next(2, 6);
        var terms = new List<int>();
        int current = start;
        for (int k = 0; k < 6; k++)
        {
            terms.Add(current);
            current *= ratio;
        }
        int correct = terms[^1] * ratio;
        string prompt = $"What is the next number in the sequence? {string.Join(", ", terms)}, ___";

        var distractors = new List<string>
        {
            (correct + ratio * 2).ToString(),
            (correct - ratio).ToString(),
            (correct + 1).ToString()
        };

        var steps = new[]
        {
            $"Observe the progression: {terms[0]} × {ratio} = {terms[1]}, {terms[1]} × {ratio} = {terms[2]}.",
            $"The constant common ratio is {ratio}.",
            $"Multiply the last term by {ratio}: {terms[^1]} × {ratio} = {correct}."
        };

        return Item(prompt, correct.ToString(), [.. distractors], $"Each term is multiplied by {ratio}. {terms[^1]} × {ratio} = {correct}.", steps);
    }

    private static PoolItem CreatePemdasQuestion(Random rng)
    {
        int c = rng.Next(2, 5);
        int qv = rng.Next(1, 11) * c;
        int d = rng.Next(2, 6);
        int a = rng.Next(10, 51);
        int correct = a + (qv / c) * d;
        string prompt = $"Simplify: {a} + {qv} ÷ {c} × {d}";

        var d1 = (a + qv / (c * d)).ToString();
        var d2 = ((a + qv) / c * d).ToString();
        var d3 = (a + qv / c + d).ToString();
        var distractors = new[] { d1, d2, d3 };

        var steps = new[]
        {
            $"Follow PEMDAS order: division and multiplication before addition.",
            $"Divide first: {qv} ÷ {c} = {qv / c}.",
            $"Multiply next: {qv / c} × {d} = {(qv / c) * d}.",
            $"Add last: {a} + {(qv / c) * d} = {correct}."
        };

        return Item(prompt, correct.ToString(), distractors, $"Order of operations: {qv} ÷ {c} = {qv / c}; {qv / c} × {d} = {(qv / c) * d}; {a} + {(qv / c) * d} = {correct}.", steps);
    }

    private static PoolItem CreateAdditionWordProblem(Random rng)
    {
        int x = rng.Next(50, 201);
        int y = rng.Next(50, 201);
        int correct = x + y;
        string prompt = $"A farmer harvested {x} mangoes on Monday and {y} mangoes on Tuesday. How many mangoes were harvested in all?";

        var steps = new[]
        {
            $"Identify Monday's harvest: {x} mangoes.",
            $"Identify Tuesday's harvest: {y} mangoes.",
            $"Calculate total: {x} + {y} = {correct} mangoes."
        };

        return Item(prompt, correct.ToString(), [(correct + 10).ToString(), (correct - 10).ToString(), (correct + 25).ToString()], $"{x} + {y} = {correct}.", steps);
    }

    private static PoolItem CreateDiscountQuestion(Random rng)
    {
        int price = rng.Next(2, 11) * 100;
        int disc = rng.Next(1, 7) * 5; // 5%, 10%, 15%, 20%, 25%, 30%
        int correct = (price * disc) / 100;
        string prompt = $"An item costs ₱{price:N0}. How much is the discount if it is discounted by {disc}%?";

        var steps = new[]
        {
            $"Convert discount rate to decimal: {disc}% = {disc / 100.0:F2}.",
            $"Multiply price by rate: ₱{price} × {disc / 100.0:F2} = ₱{correct:N0}."
        };

        return Item(prompt, $"₱{correct:N0}", [$"₱{correct + 20:N0}", $"₱{correct - 10:N0}", $"₱{correct + 50:N0}"], $"Discount = ₱{price:N0} × {disc}% = ₱{correct:N0}.", steps);
    }

    private static PoolItem CreateAverageQuestion(Random rng)
    {
        int avg = rng.Next(20, 61);
        int n1 = rng.Next(10, 51);
        int n2 = rng.Next(10, 51);
        int n3 = rng.Next(10, 51);
        int n4 = (avg * 4) - (n1 + n2 + n3);
        if (n4 < 1) n4 = 10;
        int sum = n1 + n2 + n3 + n4;
        int correct = sum / 4;
        string prompt = $"What is the average of {n1}, {n2}, {n3}, and {n4}?";

        var steps = new[]
        {
            $"Add all four numbers: {n1} + {n2} + {n3} + {n4} = {sum}.",
            $"Divide the total by the count (4): {sum} ÷ 4 = {correct}."
        };

        return Item(prompt, correct.ToString(), [(correct + 2).ToString(), (correct - 2).ToString(), (correct + 5).ToString()], $"Sum = {sum}; {sum} ÷ 4 = {correct}.", steps);
    }

    private static PoolItem CreateProportionQuestion(Random rng)
    {
        int unit = rng.Next(5, 31);
        int a = rng.Next(2, 6);
        int b = rng.Next(6, 11);
        int costA = a * unit;
        int correct = b * unit;
        string prompt = $"If {a} pens cost ₱{costA}, how much do {b} pens cost?";

        var steps = new[]
        {
            $"Find the unit price for 1 pen: ₱{costA} ÷ {a} = ₱{unit} each.",
            $"Multiply unit price by target quantity ({b}): {b} × ₱{unit} = ₱{correct}."
        };

        return Item(prompt, $"₱{correct}", [$"₱{costA}", $"₱{correct + unit}", $"₱{correct - unit}"], $"Unit price = ₱{costA} ÷ {a} = ₱{unit}; {b} × ₱{unit} = ₱{correct}.", steps);
    }

    private static PoolItem CreateChairDiscountQuestion(Random rng)
    {
        int price = rng.Next(5, 16) * 200;
        int discount = price / 5; // 20%
        int answer = price - discount;
        string prompt = $"An office chair costs ₱{price:N0}. A supplier offers a 20% discount. What is the sale price?";

        var steps = new[]
        {
            $"Convert 20% to a decimal: 20 ÷ 100 = 0.20.",
            $"Calculate the discount: ₱{price:N0} × 0.20 = ₱{discount:N0}.",
            $"Subtract discount from original price: ₱{price:N0} − ₱{discount:N0} = ₱{answer:N0}."
        };

        return Item(prompt, $"₱{answer:N0}", [$"₱{price - 50:N0}", $"₱{discount:N0}", $"₱{price + discount:N0}"], "Subtract the discount from the original price to find the sale price.", steps);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ANALYTICAL PROCEDURAL EXTENSIONS (Directions & Letter Sequences)
    // ════════════════════════════════════════════════════════════════════════
    private static List<PoolItem> GenerateAnalyticalItems(int count, Random rng)
    {
        var items = new List<PoolItem>(AnalyticalStaticPool);

        // Add dynamic directions turning problems
        string[] dirs = ["North", "East", "South", "West"];
        for (int idx = 0; idx < 4; idx++)
        {
            foreach (var turn in new[] { "left", "right" })
            {
                int step = turn == "left" ? -1 : 1;
                string ans = dirs[(idx + step + 4) % 4];
                string prompt = $"You face {dirs[idx]} and turn 90° {turn}. What direction are you facing now?";
                var d = dirs.Where(x => x != ans).ToArray();
                items.Add(Item(prompt, ans, d, $"Turning 90° {turn} from {dirs[idx]} results in facing {ans}."));
            }
        }

        // Add dynamic letter sequence problems
        for (int step = 2; step <= 3; step++)
        {
            for (int start = 0; start <= 4; start++)
            {
                var letters = new List<char>();
                for (int k = 0; k < 5; k++)
                    letters.Add((char)('A' + ((start + k * step) % 26)));
                char next = (char)('A' + ((start + 5 * step) % 26));
                string prompt = $"What letter comes next? {string.Join(", ", letters)}, ___";
                var d = new[] { ((char)('A' + (next - 'A' + 1) % 26)).ToString(), ((char)('A' + (next - 'A' + 25) % 26)).ToString(), ((char)('A' + (next - 'A' + 2) % 26)).ToString() };
                items.Add(Item(prompt, next.ToString(), d, $"The letters skip by {step} positions in the alphabet. The next letter is {next}."));
            }
        }

        return items;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC FACTORY
    // ════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Generates exactly <paramref name="count"/> randomized questions from the
    /// offline reviewer bank for the selected <paramref name="category"/>.
    /// Supports counts: 5, 10, 20, 30.
    /// Every question has its choices (A, B, C, D) shuffled so the correct answer
    /// lands on a random index (0..3).
    /// </summary>
    public static List<Question> Create(GenerateRequest r)
    {
        // Seed Random securely
        var rng = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));

        List<PoolItem> pool = r.Category switch
        {
            "Verbal Ability"      => [.. VerbalPool],
            "Numerical Ability"   => GenerateNumericalItems(Math.Max(r.Count, 36), rng),
            "Analytical Ability"  => GenerateAnalyticalItems(r.Count, rng),
            "Clerical Ability"    => [.. ClericalPool],
            "General Information" => [.. GeneralInfoPool],
            _                     => [.. GeneralInfoPool]
        };

        // Shuffle the pool and take the exact requested count (5, 10, 20, 30)
        var selected = pool
            .OrderBy(_ => rng.Next())
            .Take(r.Count)
            .ToList();

        // If the pool is smaller than count, fill with repeated shuffled variations
        if (selected.Count < r.Count)
        {
            var extra = pool.OrderBy(_ => rng.Next()).Take(r.Count - selected.Count);
            selected.AddRange(extra);
        }

        return selected.Select(item =>
        {
            // Ensure exactly 4 unique, non-empty options
            var distinctChoices = new HashSet<string>(StringComparer.Ordinal) { item.Correct };
            foreach (var d in item.Distractors)
            {
                if (!string.IsNullOrWhiteSpace(d) && !distinctChoices.Contains(d))
                {
                    distinctChoices.Add(d);
                    if (distinctChoices.Count == 4) break;
                }
            }

            int fallbackCounter = 1;
            while (distinctChoices.Count < 4)
            {
                string fallback;
                string raw = item.Correct.Trim().TrimStart('₱').Replace(",", "");
                if (int.TryParse(raw, out int numVal))
                {
                    string prefix = item.Correct.Contains("₱") ? "₱" : "";
                    fallback = $"{prefix}{numVal + fallbackCounter * 7}";
                }
                else
                {
                    fallback = $"Alternative {fallbackCounter}";
                }

                if (!distinctChoices.Contains(fallback))
                    distinctChoices.Add(fallback);

                fallbackCounter++;
            }

            // Shuffle choices so correct answer lands on random index (0..3)
            var choices = distinctChoices.OrderBy(_ => rng.Next()).ToList();
            int answerIndex = choices.IndexOf(item.Correct);

            return new Question(
                item.Prompt,
                [.. choices],
                answerIndex,
                item.Explanation,
                item.Steps
            );
        }).ToList();
    }
}

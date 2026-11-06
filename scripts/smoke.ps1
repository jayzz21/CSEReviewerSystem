param([string]$Base = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
function Assert($condition, $message) { if (!$condition) { throw $message }; Write-Output "PASS: $message" }
function Post($path, $body, $session) { Invoke-RestMethod "$Base/api$path" -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json) -WebSession $session }
function ExpectStatus($path, $body, $session, $status) {
    try { Post $path $body $session | Out-Null; throw 'Expected request failure' }
    catch { Assert ([int]$_.Exception.Response.StatusCode -eq $status) "Reject $path with $status" }
}
$a = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$b = New-Object Microsoft.PowerShell.Commands.WebRequestSession
Post '/session' @{} $a | Out-Null
Post '/session' @{} $b | Out-Null
$request = @{category='Numerical Ability';level='Professional';difficulty='Easy';count=5;focus='';mode='sample'}
$created = Post '/sets' $request $a
$set = Invoke-RestMethod "$Base/api/sets/$($created.id)" -WebSession $a
Assert ($set.questions.Count -eq 5) 'Generate five numerical questions in SQL Server'
Assert ($null -eq $set.questions[0].answer -and $null -eq $set.questions[0].explanation) 'Hide answer keys before submission'
$q = $set.questions[0]
# The first sample's correct answer is option D (400 minus 20 percent).
$correct = 3
$answer = Post "/sets/$($created.id)/answer" @{index=0;choice=$correct} $a
Assert ($answer.correct -and $answer.steps.Count -eq 3) 'Grade correct answer and provide three solving steps'
$wrong = Post "/sets/$($created.id)/answer" @{index=0;choice=0} $a
Assert (!$wrong.correct -and $wrong.answer -eq 3 -and $wrong.explanation.Length -gt 0) 'Explain incorrect answer'
Post "/sets/$($created.id)/save" @{title='Smoke test — numerical practice'} $a | Out-Null
$saved = Invoke-RestMethod "$Base/api/sets" -WebSession $a
Assert (@($saved | Where-Object id -eq $created.id).Count -eq 1) 'Save and retrieve library set'
ExpectStatus "/sets/$($created.id)/save" @{title='Unauthorized'} $b 404
ExpectStatus "/sets/$($created.id)/answer" @{index=99;choice=0} $a 400
$request.count=100
ExpectStatus '/sets' $request $a 400
$request.count=5; $request.category='Clerical Ability'
ExpectStatus '/sets' $request $a 400
foreach ($category in @('Verbal Ability','Analytical Ability','Clerical Ability','General Information')) {
    $request.category=$category; $request.level=if($category -eq 'Clerical Ability'){'Subprofessional'}else{'Professional'}
    $id=(Post '/sets' $request $a).id
    $other=Invoke-RestMethod "$Base/api/sets/$id" -WebSession $a
    Assert ($other.questions.Count -eq 5) "$category sample set has five questions"
}
Write-Output 'All smoke checks passed.'

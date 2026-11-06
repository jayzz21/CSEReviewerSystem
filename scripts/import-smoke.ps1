param([string]$Base='http://localhost:5092', [string]$ReviewerFile)
$ErrorActionPreference='Stop'
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Post($path,$data) { Invoke-RestMethod "$Base/api$path" -Method Post -WebSession $session -ContentType application/json -Body ([Text.Encoding]::UTF8.GetBytes(($data | ConvertTo-Json -Depth 15))) }
function Assert($ok,$message) { if(!$ok){throw $message}; Write-Output "PASS: $message" }
Post '/session' @{} | Out-Null
if($ReviewerFile) {
    $legacy=@{fileName='CSE_2027_Reviewer.html';html=Get-Content $ReviewerFile -Raw}
    $preview=Post '/imports/preview' $legacy
    Assert (($preview.valid+$preview.duplicates+$preview.issues.Count) -eq 540) 'Account for all 540 legacy questions'
    Write-Output ($preview | ConvertTo-Json -Depth 5)
    $import=Post '/imports' $legacy
    Assert ($import.added -eq $preview.valid -and $import.count -eq 20) 'Save legacy questions and create a 20-question set'
    $again=Post '/imports' $legacy
    Assert ($again.added -eq 0) 'Repeated import does not duplicate bank records'
    Assert ($import.totalQuestions -eq $preview.valid -and $again.totalQuestions -eq $import.totalQuestions) 'Database total counts unique questions and stays unchanged after repeat import'
    $random=Post '/bank/sets' @{category='Numerical Ability';level='Professional';difficulty='Easy';count=20}
    $set=Invoke-RestMethod "$Base/api/sets/$($random.id)" -WebSession $session
    Assert ($set.questions.Count -eq 20 -and $set.saved) 'Random database set is saved automatically'
    Assert ($null -eq $set.questions[0].answer) 'No answer keys returned before submission'
}
$template=@{fileName='template.html';html=Get-Content 'CseReviewer/wwwroot/import-template.html' -Raw}
$preview=Post '/imports/preview' $template
Assert ($preview.valid -eq 1) 'Template recognized'
$result=Post '/imports' $template
$set=Invoke-RestMethod "$Base/api/sets/$($result.id)" -WebSession $session
$choice=[array]::IndexOf($set.questions[0].options,'60')
$feedback=Post "/sets/$($result.id)/answer" @{index=0;choice=$choice}
Assert ($feedback.correct -and $feedback.steps.Count -eq 2) 'Shuffled choices retain correct answer and steps'
$other=New-Object Microsoft.PowerShell.Commands.WebRequestSession
Invoke-RestMethod "$Base/api/session" -Method Post -WebSession $other -ContentType application/json -Body '{}' | Out-Null
$private=Invoke-RestMethod "$Base/api/bank" -WebSession $other
Assert (@($private).Count -eq 0) 'Imported bank is isolated by profile'
try { Post '/imports/preview' @{fileName='unsafe.html';html='<script>throw new Error("must never execute");</script>'} | Out-Null; throw 'Unsupported script accepted' }
catch { Assert ([int]$_.Exception.Response.StatusCode -eq 400) 'Unknown JavaScript HTML rejected without execution' }
try { Post '/bank/sets' @{category='Numerical Ability';level='Professional';difficulty='Hard';count=30} | Out-Null; throw 'Insufficient bank accepted' }
catch { Assert ([int]$_.Exception.Response.StatusCode -eq 400) 'Insufficient matching bank returns actionable error' }
Write-Output 'Import integration checks passed.'

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$errors = [System.Collections.Generic.List[string]]::new()
$projectFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -Filter '*.csproj' -File)

function Get-RelativePath {
    param([string] $BasePath, [string] $TargetPath)
    $baseUri = [Uri]::new($BasePath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar)
    $targetUri = [Uri]::new($TargetPath)
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

foreach ($projectFile in $projectFiles) {
    [xml] $project = Get-Content -LiteralPath $projectFile.FullName -Raw
    foreach ($reference in $project.SelectNodes('//ProjectReference')) {
        $include = $reference.GetAttribute('Include')
        if ($include -match '\$\(GmaModule(?!OrganizationsRoot\))') {
            $relativeProject = Get-RelativePath -BasePath $repositoryRoot -TargetPath $projectFile.FullName
            $errors.Add("$relativeProject references another reusable module through '$include'.")
        }
    }
}

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
foreach ($sourceFile in $sourceFiles) {
    $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
    $relativePath = Get-RelativePath -BasePath $repositoryRoot -TargetPath $sourceFile.FullName
    if ($source -match 'Gma\.Modules\.(?!Organizations(?:\.|;))') {
        $errors.Add("$relativePath names another reusable module implementation or contract.")
    }

    if ($source -match '(?:BunkFy|StayQuest)\.') {
        $errors.Add("$relativePath contains product-specific source.")
    }

    if ($relativePath -match '^src\\Gma\.Modules\.Organizations\.(?:Api|AdminApi|AdminCli)\\' -and
        $source -match 'Gma\.Modules\.Organizations\.Domain') {
        $errors.Add("$relativePath crosses the front-door to domain boundary.")
    }
}

$commandHandlerDirectory = Join-Path $repositoryRoot 'src/Gma.Modules.Organizations.Application/Handlers'
$commandHandlers = @(Get-ChildItem -LiteralPath $commandHandlerDirectory -Filter '*CommandHandler.cs' -File)
$sharedGovernanceHandlers = @(
    'AcceptOrganizationInvitationCommandHandler',
    'ClaimOrganizationEnrollmentLinkCommandHandler',
    'DisableOrganizationEnrollmentLinkCommandHandler',
    'IssueOrganizationEnrollmentLinkCommandHandler',
    'IssueOrganizationInvitationCommandHandler',
    'ReissueOrganizationInvitationCommandHandler',
    'ResolveOrganizationJoinRequestCommandHandler',
    'RevokeOrganizationInvitationCommandHandler',
    'RotateOrganizationEnrollmentLinkCommandHandler',
    'UpdateOrganizationCommandHandler'
)
$exclusiveGovernanceHandlers = @(
    'ChangeOrganizationLifecycleCommandHandler',
    'ChangeOrganizationLifecycleForAdministrationCommandHandler',
    'ChangeOrganizationMembershipCommandHandler',
    'EnsureOrganizationMembershipStateCommandHandler',
    'EnsureOrganizationOwnerForAdministrationCommandHandler',
    'TransferOrganizationOwnershipCommandHandler'
)
$uncoordinatedHandlers = @(
    'CreateOrganizationCommandHandler',
    'ExpireOrganizationEnrollmentClaimsCommandHandler',
    'ExpireOrganizationEnrollmentLinksCommandHandler',
    'ExpireOrganizationInvitationsCommandHandler'
)
$classifiedHandlers = @(
    $sharedGovernanceHandlers
    $exclusiveGovernanceHandlers
    $uncoordinatedHandlers
)

foreach ($duplicate in @($classifiedHandlers | Group-Object | Where-Object Count -gt 1)) {
    $errors.Add("Command handler '$($duplicate.Name)' has more than one governance classification.")
}

foreach ($handler in $commandHandlers) {
    if ($handler.BaseName -notin $classifiedHandlers) {
        $errors.Add("$($handler.Name) has no explicit governance coordination classification.")
    }
}

foreach ($handlerName in $classifiedHandlers) {
    $handler = $commandHandlers | Where-Object BaseName -eq $handlerName
    if ($null -eq $handler) {
        $errors.Add("Governance coordination classifies missing command handler '$handlerName'.")
        continue
    }

    $source = Get-Content -LiteralPath $handler.FullName -Raw
    $sharedCount = [regex]::Matches($source, 'governance\.AcquireSharedAsync\(').Count
    $exclusiveCount = [regex]::Matches($source, 'governance\.AcquireExclusiveAsync\(').Count
    if ($handlerName -in $sharedGovernanceHandlers -and
        ($sharedCount -ne 1 -or $exclusiveCount -ne 0)) {
        $errors.Add("$($handler.Name) must acquire shared governance exactly once.")
    }
    elseif ($handlerName -in $exclusiveGovernanceHandlers -and
        ($exclusiveCount -ne 1 -or $sharedCount -ne 0)) {
        $errors.Add("$($handler.Name) must acquire exclusive governance exactly once.")
    }
    elseif ($handlerName -in $uncoordinatedHandlers -and
        ($sharedCount -ne 0 -or $exclusiveCount -ne 0)) {
        $errors.Add("$($handler.Name) is classified as intentionally uncoordinated but acquires governance.")
    }

    if ($handlerName -in $sharedGovernanceHandlers -or
        $handlerName -in $exclusiveGovernanceHandlers) {
        $commandName = $handlerName -replace 'Handler$', ''
        $commandFile = Join-Path $repositoryRoot "src/Gma.Modules.Organizations.Application/Commands/$commandName.cs"
        if (-not (Test-Path -LiteralPath $commandFile)) {
            $errors.Add("$($handler.Name) has no matching transactional command source '$commandName.cs'.")
        }
        else {
            $commandSource = Get-Content -LiteralPath $commandFile -Raw
            if ($commandSource -notmatch 'ITransactionalCommand\s*<') {
                $errors.Add("$commandName must remain transactional while it acquires governance.")
            }
        }

        $acquisitionMatch = [regex]::Match(
            $source,
            'governance\.Acquire(?:Shared|Exclusive)Async\(')
        $protectedReadMatch = [regex]::Match(
            $source,
            'OrganizationMembershipAuthorization\.Require|joinSourceAuthorization\.AuthorizeAsync|organizations\s*\.\s*Get(?:Organization|Membership)Async')
        if ($protectedReadMatch.Success -and
            $acquisitionMatch.Index -gt $protectedReadMatch.Index) {
            $errors.Add("$($handler.Name) reads governance before acquiring its transaction lock.")
        }

        $sourceLockMatch = [regex]::Match(
            $source,
            'issuance\.Acquire(?:Invitation|EnrollmentLink|Replacement)Async')
        if ($sourceLockMatch.Success -and
            $acquisitionMatch.Index -gt $sourceLockMatch.Index) {
            $errors.Add("$($handler.Name) acquires a join-source lock before organization governance.")
        }
    }
}

if ($errors.Count -gt 0) {
    throw "Organizations boundary checks failed:`n - $($errors -join "`n - ")"
}

Write-Host 'Organizations boundary checks passed.'

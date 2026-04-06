using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.Services;

/// <summary>
/// Performs a dry-run evaluation of all rules against the source and destination.
/// NEVER writes to the filesystem — only reads and builds a plan.
/// </summary>
public class ValidationService
{
    /// <summary>
    /// Evaluates every rule and returns an UpgradePlan describing what
    /// ExecuteUpgrade will do. Safe to call multiple times.
    /// </summary>
    public UpgradePlan Validate(
        SourceInfo       source,
        string           destinationRoot,
        List<UpdateRule> rules,
        Action<string>   log)
    {
        var plan = new UpgradePlan();

        log("[VALIDATE] Starting dry-run check...");

        if (string.IsNullOrWhiteSpace(source.ExtractedPath) ||
            !Directory.Exists(source.ExtractedPath))
        {
            plan.IsValid = false;
            plan.Issues.Add($"Source path does not exist: '{source.ExtractedPath}'");
            log($"[VALIDATE] ERROR: {plan.Issues[^1]}");
            return plan;
        }

        if (!Directory.Exists(destinationRoot))
        {
            plan.Issues.Add($"WARN: Destination '{destinationRoot}' does not exist and will be created.");
            log($"[VALIDATE] {plan.Issues[^1]}");
        }

        if (rules.Count == 0)
        {
            plan.IsValid = false;
            plan.Issues.Add("No rules defined. Add at least one rule.");
            log($"[VALIDATE] ERROR: {plan.Issues[^1]}");
            return plan;
        }

        foreach (var rule in rules)
        {
            log($"[VALIDATE] Checking rule: {rule}");
            EvaluateRule(rule, source.ExtractedPath, destinationRoot, plan, log);
        }

        log($"[VALIDATE] Plan: {plan.FilesCopied} files, {plan.DirsCreated} dirs, " +
            $"{plan.PermsChanged} permission changes. Issues: {plan.Issues.Count}");

        return plan;
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static void EvaluateRule(
        UpdateRule   rule,
        string       sourceRoot,
        string       destRoot,
        UpgradePlan  plan,
        Action<string> log)
    {
        var srcPath  = BuildPath(sourceRoot, rule.SourceRelativePath);
        var destPath = BuildPath(destRoot,   rule.DestinationRelativePath);
        var permissions = rule.GetEffectivePermissions();

        switch (rule.Operation)
        {
            case OperationType.RecursiveCopy:
                if (!Directory.Exists(srcPath))
                {
                    plan.Issues.Add($"WARN [{rule.Name}]: Source folder not found: {srcPath}");
                    log($"[VALIDATE]   WARN: {plan.Issues[^1]}");
                    return;
                }
                PlanDirectory(srcPath, destPath, permissions, recursive: true, plan, log);
                break;

            case OperationType.FilesOnly:
                if (!Directory.Exists(srcPath))
                {
                    plan.Issues.Add($"WARN [{rule.Name}]: Source folder not found: {srcPath}");
                    log($"[VALIDATE]   WARN: {plan.Issues[^1]}");
                    return;
                }
                PlanDirectory(srcPath, destPath, permissions, recursive: false, plan, log);
                break;

            case OperationType.ReplaceFile:
                if (!File.Exists(srcPath))
                {
                    plan.Issues.Add($"WARN [{rule.Name}]: Source file not found: {srcPath}");
                    log($"[VALIDATE]   WARN: {plan.Issues[^1]}");
                    return;
                }
                PlanFile(srcPath, destPath, permissions, plan);
                break;
        }
    }

    private static void PlanDirectory(
        string srcDir, string destDir, string permissions,
        bool recursive, UpgradePlan plan, Action<string> log)
    {
        if (!plan.Actions.Any(a => a.Kind == ActionKind.CreateDirectory
                                && a.DestinationPath == destDir))
        {
            plan.Actions.Add(new PlannedAction
            {
                Kind            = ActionKind.CreateDirectory,
                DestinationPath = destDir
            });
        }

        foreach (var file in Directory.EnumerateFiles(srcDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            PlanFile(file, destFile, permissions, plan);
        }

        if (!recursive) return;

        foreach (var subDir in Directory.EnumerateDirectories(srcDir))
        {
            var subDest = Path.Combine(destDir, Path.GetFileName(subDir));
            PlanDirectory(subDir, subDest, permissions, recursive: true, plan, log);
        }
    }

    private static void PlanFile(
        string srcFile, string destFile, string permissions, UpgradePlan plan)
    {
        plan.Actions.Add(new PlannedAction
        {
            Kind            = ActionKind.CopyFile,
            SourcePath      = srcFile,
            DestinationPath = destFile
        });

        if (!string.IsNullOrWhiteSpace(permissions))
        {
            plan.Actions.Add(new PlannedAction
            {
                Kind            = ActionKind.SetPermissions,
                DestinationPath = destFile,
                Permissions     = permissions
            });
        }
    }

    private static string BuildPath(string root, string relative) =>
        string.IsNullOrWhiteSpace(relative)
            ? root
            : Path.Combine(root, relative.TrimStart('/', '\\'));
}

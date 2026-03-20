using TaskFlowApp.Models.ProjectManagement;

namespace TaskFlowApp.Infrastructure.Helpers;

public static class TaskHelper
{
    private static readonly HashSet<string> PriorityValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Öncelikli Görev",
        "Sıradan Görev",
        "Acil Görev"
    };

    public static bool IsPriorityValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return PriorityValues.Contains(value.Trim());
    }

    public static CompanyTaskDto NormalizeCategoryAndPriority(CompanyTaskDto task)
    {
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName)
            ? "Bireysel"
            : task.CategoryName.Trim();

        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName)
            ? "Belirtilmedi"
            : task.TaskPriorityName.Trim();

        if (IsPriorityValue(categoryName))
        {
            if (!IsPriorityValue(priorityName))
            {
                priorityName = categoryName;
            }

            categoryName = "Bireysel";
        }

        return task with
        {
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        };
    }

    public static CompanyTaskDto MapIndividualTask(IndividualTaskDto task)
    {
        var statusName = string.IsNullOrWhiteSpace(task.StatusName) ? TaskStatusHelper.DefaultOpenStatus : task.StatusName;
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName) ? "Bireysel" : task.CategoryName;
        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName) ? "Belirtilmedi" : task.TaskPriorityName;

        return NormalizeCategoryAndPriority(new CompanyTaskDto
        {
            TaskName = task.TaskTitle,
            Description = task.Description,
            DeadlineTime = task.Deadline,
            StatusName = statusName,
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        });
    }

    public static List<CompanyTaskDto> OrderTasks(IEnumerable<CompanyTaskDto> tasks)
    {
        return tasks
            .OrderBy(task => TaskStatusHelper.IsCompletedStatus(task.StatusName))
            .ThenBy(task => task.DeadlineTime)
            .ThenBy(task => task.TaskName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

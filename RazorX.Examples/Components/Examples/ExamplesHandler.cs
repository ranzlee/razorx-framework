using RazorX.Framework;
using RazorX.Examples.Components.Layout;
using Microsoft.AspNetCore.Mvc;

namespace RazorX.Examples.Components.Examples;

public class TodoModel(int id, string text, bool isComplete) {
    public int Id { get; set; } = id;
    public string Text { get; set; } = text;
    public bool IsComplete { get; set; } = isComplete;
};

public record ExampleModel(IEnumerable<TodoModel> Todos, int Total, int Completed, FileUploadModel FileUpload);
public record TodoFormModel(int Id, string Text, bool IsComplete, bool HasError, bool IsEdit);
public record FileUploadModel(string FileId, string FileName, string NewFileName, Stream? File);

public class ExamplesHandler : RequestHandler {
    public override void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples", Get);
        router.MapPost("/todo", NewTodo);
        router.MapPut("/todo/{id:int}", SaveTodo);
        router.MapGet("/new-todo-reset", ResetNewTodo);
        router.MapGet("/edit-todo-reset", ResetEditTodo);
        router.MapGet("/todo/{id:int}", EditTodo);
        router.MapGet("/todo/next/{id:int}", NextTodos);
        router.MapDelete("/todo/{id:int}", DeleteTodo);
        router.MapGet("/search-todos", SearchTodos);
        router.MapGet("/poll-test", PollTest);
        router.MapPost("/file-upload", FileUpload);
        router.MapPost("/file-form-submit", FileUploadFormSubmit);
        router.MapDelete("/file-reset", FileUploadReset);
    }

    private static readonly List<TodoModel> Todos = [];
    private static FileUploadModel Upload = new(string.Empty, string.Empty, string.Empty, null);

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver, string filter = "") {
        return await rxDriver.RenderPage<App, ExamplesHead, ExamplesPage, ExampleModel>(context, new ExampleModel([], 0, 0, Upload), "RazorX - Examples");
    }

    public static async Task<IResult> NextTodos(HttpContext context, IRxDriver rxDriver, int id, string filter = "") {
        var page = Todos
            .Where(x => x.Id < id && x.Text.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x => x.Id)
            .Take(5);
        return await rxDriver
            .With(context)
            .AddTriggerToast("Todos loaded!", ToastType.Info, 3500, ToastVerticalPosition.Bottom, ToastHorizontalPosition.Right)
            .AddFragment<TodoList, IEnumerable<TodoModel>>(page, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
            .Render();
    }

    public static async Task<IResult> SearchTodos(HttpContext context, IRxDriver rxDriver, string filter = "") {
        var page = Todos
            .Where(x => x.Text.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x => x.Id)
            .Take(5);
        var driver = rxDriver
            .With(context)
            .AddTriggerSetState("filter", filter, MetadataScope.Session, true)
            .AddFragment<TodoSearch, string>(filter, "search-todos", FragmentMergeStrategyType.Morph)
            .AddFragment<TodoList, IEnumerable<TodoModel>>(page, "todo-list", FragmentMergeStrategyType.SwapInner)
            .AddFragment<TodoCount, (int Completed, int Total)>(GetCount(), "todo-count", FragmentMergeStrategyType.Swap);
        if (!string.IsNullOrWhiteSpace(filter)) {
            driver.AddTriggerToast("Filter applied!", ToastType.Warning, 3500, ToastVerticalPosition.Top, ToastHorizontalPosition.Left);
        }
        return await driver.Render();
    }

    public static IResult PollTest() {
        return TypedResults.NoContent();
    }

    public static async Task<IResult> NewTodo(HttpContext context, IRxDriver rxDriver, TodoFormModel model) {
        var validationResult = ValidateTodo(context, rxDriver, false, model);
        if (validationResult != null) {
            return await validationResult.Render();
        }
        var todo = new TodoModel(Todos.Count == 0 ? 1 : Todos.Max(x => x.Id + 1), model.Text, false);
        Todos.Add(todo);
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("new-todo-modal")
            .AddTriggerFocusElement("new-todo-modal-trigger")
            .AddTriggerToast("Todo created successfully!", ToastType.Success)
            .AddFragment<TodoForm, TodoFormModel>(new TodoFormModel(0, "", false, false, false), "new-todo-form", FragmentMergeStrategyType.Swap)
            .AddFragment<TodoItem, TodoModel>(todo, "todo-list", FragmentMergeStrategyType.AppendAfterBegin)
            .AddFragment<TodoCount, (int Completed, int Total)>(GetCount(), "todo-count", FragmentMergeStrategyType.Swap)
            .Render();
    }

    public static async Task<IResult> SaveTodo(HttpContext context, IRxDriver rxDriver, TodoFormModel model, int id) {
        await Task.Delay(700);
        model = model with { Id = id };
        var validationResult = ValidateTodo(context, rxDriver, true, model);
        if (validationResult != null) {
            return await validationResult.Render();
        }
        var todo = Todos.FirstOrDefault(x => x.Id == id);
        if (todo == null) {
            return TypedResults.Accepted("/error?code=404");
        }
        todo.Text = model.Text;
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("edit-todo-modal")
            .AddTriggerFocusElement($"edit-todo-modal-trigger-{id}")
            .AddTriggerToast("Todo updated successfully!", ToastType.Success)
            .AddFragment<TodoFormLoading>("edit-todo-form-container", FragmentMergeStrategyType.SwapInner)
            .AddFragment<TodoItem, TodoModel>(todo, $"todo-item-{todo.Id}", FragmentMergeStrategyType.Swap)
            .Render();
    }

    public static async Task<IResult> ResetNewTodo(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddFragment<TodoForm, TodoFormModel>(new TodoFormModel(0, "", false, false, false), "new-todo-form", FragmentMergeStrategyType.Swap)
            .Render();
    }

    public static async Task<IResult> ResetEditTodo(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("edit-todo-modal")
            .AddFragment<TodoFormLoading>("edit-todo-form-container", FragmentMergeStrategyType.SwapInner)
            .Render();
    }

    public static async Task<IResult> DeleteTodo(HttpContext context, IRxDriver rxDriver, int id, string filter = "") {
        var todo = Todos.SingleOrDefault(x => x.Id == id);
        if (todo == null) {
            return TypedResults.Accepted("/error?code=404");
        }
        var todos = Todos
            .Where(x => x.Text.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x => x.Id)
            .ToList();
        todos.Remove(todo);
        Todos.Remove(todo);
        var driver = rxDriver
            .With(context)
            .AddTriggerCloseDialog("delete-todo-modal")
            .AddTriggerToast("Todo deleted successfully!", ToastType.Success)
            .AddFragment<TodoCount, (int Completed, int Total)>(GetCount(), "todo-count", FragmentMergeStrategyType.Swap)
            .RemoveElement($"todo-item-{id}");
        if (todos.Count == 0) {
            driver.AddTriggerFocusElement("new-todo-modal-trigger", true);
        } else {
            var nextFocus = todos.OrderBy(x => x.Id).FirstOrDefault(x => x.Id > id) ?? todos.FirstOrDefault(x => x.Id < id);
            var nextFocusId = nextFocus?.Id ?? todos.First().Id;
            driver.AddTriggerFocusElement($"edit-todo-modal-trigger-{nextFocusId}");
        }
        return await driver.Render();
    }

    public static async Task<IResult> EditTodo(HttpContext context, IRxDriver rxDriver, int id) {
        await Task.Delay(500);
        var todo = Todos.FirstOrDefault(x => x.Id == id);
        if (todo == null) {
            return TypedResults.Accepted("/error?code=404");
        }
        var model = new TodoFormModel(todo.Id, todo.Text, todo.IsComplete, false, true);
        return await rxDriver
            .With(context)
            .AddTriggerFocusElement($"todo-text-{id}", true)
            .AddFragment<TodoForm, TodoFormModel>(model, "edit-todo-form-container", FragmentMergeStrategyType.SwapInner)
            .Render();
    }

    private static IRxResponseBuilder? ValidateTodo(HttpContext context, IRxDriver rxDriver, bool isEdit, TodoFormModel model) {
        if (string.IsNullOrWhiteSpace(model.Text)) {
            model = model with { IsEdit = isEdit, HasError = true };
            return rxDriver
                .With(context)
                .AddTriggerToast("Validation error!", ToastType.Error, 3500, ToastVerticalPosition.Top, ToastHorizontalPosition.Middle)
                .AddFragment<TodoForm, TodoFormModel>(model, isEdit ? "edit-todo-form" : "new-todo-form", FragmentMergeStrategyType.Swap);
        }
        return null;
    }

    private static (int Completed, int Total) GetCount() {
        return new(Todos.Count(x => x.IsComplete), Todos.Count);
    }

    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public static async Task<IResult> FileUpload(HttpContext context, IRxDriver rxDriver, IFormFile uploadedFile) {
        var s = new MemoryStream();
        await uploadedFile.CopyToAsync(s);
        Upload = new(Guid.NewGuid().ToString(), uploadedFile.FileName, string.Empty, s);
        return await rxDriver
            .With(context)
            .AddFragment<FileUploaded, FileUploadModel>(Upload, "file-upload-container", FragmentMergeStrategyType.Swap)
            .Render();
    }

    public static async Task<IResult> FileUploadFormSubmit(HttpContext context, IRxDriver rxDriver, FileUploadModel model) {
        if (string.IsNullOrWhiteSpace(model.FileId)) {
            return await rxDriver
                .With(context)
                .AddTriggerToast("File is required", ToastType.Error)
                .Render();
        }
        if (string.IsNullOrWhiteSpace(model.NewFileName)) {
            return await rxDriver
                .With(context)
                .AddTriggerToast("A new file name was not provided", ToastType.Warning)
                .Render();
        }
        Upload = Upload with { FileName = model.NewFileName, NewFileName = model.NewFileName };
        return await rxDriver
            .With(context)
            .AddTriggerToast("Form submitted successfully!", ToastType.Success)
            .AddFragment<FileUploaded, FileUploadModel>(Upload, "file-form", FragmentMergeStrategyType.Swap)
            .Render();
    }

    public static async Task<IResult> FileUploadReset(HttpContext context, IRxDriver rxDriver) {
        var fullReset = !string.IsNullOrWhiteSpace(Upload.NewFileName);
        Upload = new(string.Empty, string.Empty, string.Empty, null);
        return await rxDriver
            .With(context)
            .AddTriggerToast("File upload was reset", ToastType.Success)
            .AddFragment<FileForm, FileUploadModel>(Upload, fullReset ? "file-upload-container" : "file-form", FragmentMergeStrategyType.Swap)
            .Render();
    }
}

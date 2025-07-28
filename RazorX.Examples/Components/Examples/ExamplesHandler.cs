using RazorX.Framework;
using RazorX.Examples.Components.Layout;

namespace RazorX.Examples.Components.Examples;

public class TodoModel(int id, string text, bool isComplete) {
    public int Id { get; set; } = id;
    public string Text { get; set; } = text;
    public bool IsComplete { get; set; } = isComplete;
};

public record ExampleModel(IEnumerable<TodoModel> Todos);
public record TodoFormModel(int Id, string Text, bool IsComplete, bool HasError, bool IsEdit);

public class ExamplesHandler : IRequestHandler {
    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples", Get);
        router.MapPost("/todo", NewTodo);
        router.MapPut("/todo/{id:int}", SaveTodo);
        router.MapGet("/new-todo-reset", ResetNewTodo);
        router.MapGet("/edit-todo-reset", ResetEditTodo);
        router.MapGet("/todo/{id:int}", EditTodo);
        router.MapDelete("/todo/{id:int}", DeleteTodo);
    }

    private static readonly List<TodoModel> Todos = [];

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, ExamplesHead, ExamplesPage, ExampleModel>(new ExampleModel(Todos), "RazorX - Examples")
            .Render();
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
            .AddFragment<TodoForm, TodoFormModel>(new TodoFormModel(0, "", false, false, false), "new-todo-form", FragmentMergeStrategyType.Swap)
            .AddFragment<TodoItem, TodoModel>(todo, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
            .Render();
    }

    public static async Task<IResult> SaveTodo(HttpContext context, IRxDriver rxDriver, TodoFormModel model, int id) {
        model = model with { Id = id };
        var validationResult = ValidateTodo(context, rxDriver, true, model);
        if (validationResult != null) {
            return await validationResult.Render();
        }
        var todo = Todos.FirstOrDefault(x => x.Id == id);
        if (todo == null) {
            return TypedResults.NotFound();
        }
        todo.Text = model.Text;
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("edit-todo-modal")
            .AddTriggerFocusElement("search-todos", true)
            .RemoveElement("edit-todo-form")
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
            .RemoveElement("edit-todo-form")
            .Render();
    }

    public static async Task<IResult> DeleteTodo(HttpContext context, IRxDriver rxDriver, int id) {
        var todo = Todos.SingleOrDefault(x => x.Id == id);
        if (todo == null) {
            return TypedResults.NotFound();
        }
        Todos.Remove(todo);
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("delete-todo-modal")
            .RemoveElement($"todo-item-{id}")
            .AddTriggerFocusElement("search-todos", true)
            .Render();
    }

    public static async Task<IResult> EditTodo(HttpContext context, IRxDriver rxDriver, int id) {
        var todo = Todos.FirstOrDefault(x => x.Id == id);
        if (todo == null) {
            return TypedResults.NotFound();
        }
        var model = new TodoFormModel(todo.Id, todo.Text, todo.IsComplete, false, true);
        return await rxDriver
            .With(context)
            .AddTriggerFocusElement($"todo-text-{id}", true)
            .AddFragment<TodoForm, TodoFormModel>(model, "edit-todo-form-container", FragmentMergeStrategyType.AppendAfterBegin)
            .Render();
    }

    private static IRxResponseBuilder? ValidateTodo(HttpContext context, IRxDriver rxDriver, bool isEdit, TodoFormModel model) {
        if (string.IsNullOrWhiteSpace(model.Text)) {
            model = model with { IsEdit = isEdit, HasError = true };
            return rxDriver
                .With(context)
                .AddFragment<TodoForm, TodoFormModel>(model, isEdit ? "edit-todo-form" : "new-todo-form", FragmentMergeStrategyType.Morph);
        }
        return null;
    }
}

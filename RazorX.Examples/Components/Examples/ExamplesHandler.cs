using RazorX.Framework;
using RazorX.Examples.Components.Layout;

namespace RazorX.Examples.Components.Examples;

public record ExampleModel (IEnumerable<TodoModel> Todos);
public record TodoModel(int Id, string Text, bool IsComplete);

public class ExamplesHandler : IRequestHandler {
    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples", Get);
        router.MapPost("/todo", NewTodo);
        router.MapDelete("/todo/{id:int}", DeleteTodo);
    }

    private static readonly List<TodoModel> Todos = [];

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, ExamplesHead, ExamplesPage, ExampleModel>(new ExampleModel(Todos), "RazorX - Examples")
            .Render();
    }

    public static async Task<IResult> NewTodo(HttpContext context, IRxDriver rxDriver, TodoModel model) {
        model = model with { Id = Todos.Count == 0 ? 1 : Todos.Max(x => x.Id + 1) };
        Todos.Add(model);
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("new-todo-modal", null, "todo-form")
            .AddFragment<TodoItem, TodoModel>(model, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
            .Render();
    }

    public static async Task<IResult> DeleteTodo(HttpContext context, IRxDriver rxDriver, int id) {
        Todos.Remove(Todos.Single(x => x.Id == id));
        return await rxDriver
            .With(context)
            .AddTriggerCloseDialog("delete-todo-modal")
            .RemoveElement($"todo-item-{id}")
            .Render();
    }
}

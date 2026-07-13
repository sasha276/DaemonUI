using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;

namespace testdaemon.Service;

public static class TreeDataGridCommands
{
    public static HierarchicalTreeDataGridSource<CommandNode> CreateTreeDataGridSource(
        ObservableCollection<CommandNode> commands,
        Action<CommandNode> onSend)
    {
        var source = new HierarchicalTreeDataGridSource<CommandNode>(commands)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<CommandNode>(
                    new TextColumn<CommandNode, string>("Название", x => x.Name),
                    x => x.Children),

                new TextColumn<CommandNode, string>("Id", x => x.Id),
                new TextColumn<CommandNode, string>("Body", x => x.Body),

                new TemplateColumn<CommandNode>(
                    "Действие",
                    new FuncDataTemplate<CommandNode>((node, _) =>
                    {
                        if (node == null || node.IsGroup) return null;

                        var button = new Button { Content = "Send" };
                        button.Click += (_, _) => onSend(node);
                        return button;
                    }),
                    width: GridLength.Auto),
            }
        };

        source.RowSelection!.SingleSelect = false;
        return source;
    }
}
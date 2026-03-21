namespace Kopilot;

public partial class UserInputDialog : Form
{
    private readonly UserInputEventArgs _args;

    public UserInputDialog(UserInputEventArgs args)
    {
        _args = args;
        InitializeComponent();
        PopulateQuestion();
    }

    private void PopulateQuestion()
    {
        labelQuestion.Text = _args.Question;

        if (_args.Choices is { Count: > 0 })
        {
            listBoxChoices.Items.Clear();
            foreach (var choice in _args.Choices)
                listBoxChoices.Items.Add(choice);
            listBoxChoices.Visible = true;
            listBoxChoices.SelectedIndex = 0;
            labelOrType.Visible = _args.AllowFreeform;
            textBoxAnswer.Visible = _args.AllowFreeform;
        }
        else
        {
            listBoxChoices.Visible = false;
            labelOrType.Visible = false;
            textBoxAnswer.Visible = true;
            textBoxAnswer.Focus();
        }
    }

    private void ButtonSubmit_Click(object? sender, EventArgs e)
    {
        string answer;

        if (listBoxChoices.Visible && listBoxChoices.SelectedItem is string selected && !string.IsNullOrEmpty(textBoxAnswer.Text) == false)
            answer = selected;
        else if (!string.IsNullOrWhiteSpace(textBoxAnswer.Text))
            answer = textBoxAnswer.Text.Trim();
        else if (listBoxChoices.SelectedItem is string sel)
            answer = sel;
        else
            answer = "";

        _args.Answer.TrySetResult(answer);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void TextBoxAnswer_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.Handled = true;
            ButtonSubmit_Click(sender, e);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_args.Answer.Task.IsCompleted)
        {
            string fallback = listBoxChoices.SelectedItem as string ?? "";
            _args.Answer.TrySetResult(fallback);
        }
        base.OnFormClosing(e);
    }
}

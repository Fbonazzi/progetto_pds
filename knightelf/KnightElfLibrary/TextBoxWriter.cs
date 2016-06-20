using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace KnightElfLibrary
{
    /// <summary>
    /// TextWriter that allows writing asynchronously on a TextBox.
    /// </summary>
    /// <seealso cref="System.IO.TextWriter" />
    public class TextBoxWriter : TextWriter
    {
        TextBox textBox = null;

        public TextBoxWriter(TextBox tb)
        {
            textBox = tb;
        }

        public override void Write(char value)
        {
            base.Write(value);

            //Dispatcher prevents threading problems.
            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.AppendText(value.ToString());
                ScrollViewer sv = (ScrollViewer) textBox.Parent;
                sv.ScrollToBottom();
            }));
        }

        public override Encoding Encoding
        {
            get { return System.Text.Encoding.UTF8; }
        }
    }
}

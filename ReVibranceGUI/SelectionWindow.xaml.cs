using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ReVibranceGUI
{
    public partial class SelectionWindow : Window
    {
        public string SelectedItem { get; private set; }

        public SelectionWindow(string title, string header, IEnumerable<string> items)
        {
            InitializeComponent();
            this.Title = title;
            HeaderTextBlock.Text = header;
            ItemListBox.ItemsSource = items;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (ItemListBox.SelectedItem != null)
            {
                SelectedItem = ItemListBox.SelectedItem.ToString();
                DialogResult = true;
            }
            else
            {
                System.Windows.MessageBox.Show("Please select an item first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ItemListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemListBox.SelectedItem != null)
            {
                SelectedItem = ItemListBox.SelectedItem.ToString();
                DialogResult = true;
            }
        }
    }
}

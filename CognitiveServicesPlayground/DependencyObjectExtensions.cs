using Windows.UI.Xaml.Media;

namespace Windows.UI.Xaml
{
    public static class DependencyObjectExtensions
    {
        public static T FindVisualChild<T>(this DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T) return (T)child;

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }


        public static T FindVisualParent<T>(this DependencyObject obj)
            where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(obj);
            if (parent == default(T))
            {
                return default(T);
            }

            if (parent is T) return (T)parent;
            return FindVisualParent<T>(parent);
        }
    }
}
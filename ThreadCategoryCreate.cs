using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WpfApp3
{
    public class ThreadCategoryCreate
    {
        string categoryName;
        int layersCount;
        private ExampleCallback callback;

        public ThreadCategoryCreate(string categoryName, int layersCount, ExampleCallback delegateCall) 
        {
            this.categoryName = categoryName;
            this.layersCount = layersCount;
            callback = delegateCall;
        }
        public void beginProc()
        {
            callback(categoryName);
        }
    }

    public delegate void ExampleCallback(string layersCount);
}
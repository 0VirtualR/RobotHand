using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RobotHand_20260313.Extensions
{
    public static class SnackbarExtensions
    {
        private static Snackbar _snackbar;

        public static void Init(Snackbar snackbar)
        {
            _snackbar = snackbar;
        }

        public static void Show(string msg)
        {
            _snackbar?.MessageQueue?.Enqueue(msg);
        }
    }
}

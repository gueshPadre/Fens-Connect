using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Text;

namespace FENS_Connect.Platforms.Android
{

    [BroadcastReceiver(
        Enabled =true, 
        Exported = true,
       Label = "Alone Widget")]
    [IntentFilter(new[] {AppWidgetManager.ActionAppwidgetUpdate})]
    [MetaData(
        "android.appwidget.provider",
        Resource = "@xml/alone_widget_info")]
    internal class AloneWidget: AppWidgetProvider
    {
        public override void OnUpdate(
           Context context,
           AppWidgetManager appWidgetManager,
           int[] appWidgetIds)
        {
            foreach (var widgetId in appWidgetIds)
            {
                RemoteViews views = new RemoteViews(
                    context.PackageName,
                    Resource.Layout.alone_widget);

                appWidgetManager.UpdateAppWidget(widgetId, views);
            }
        }
    }
}

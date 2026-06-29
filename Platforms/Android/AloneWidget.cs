using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

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
    public class AloneWidget: AppWidgetProvider
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            base.OnReceive(context, intent);

            if (context == null || intent?.Action != AloneModeState.WidgetToggleAction)
            {
                return;
            }

            AloneModeState.Toggle();

            var appWidgetManager = AppWidgetManager.GetInstance(context);
            var widget = new ComponentName(context, Java.Lang.Class.FromType(typeof(AloneWidget)));
            var appWidgetIds = appWidgetManager.GetAppWidgetIds(widget);

            OnUpdate(context, appWidgetManager, appWidgetIds);
        }

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

                var intent = new Intent(context, typeof(AloneWidget));
                intent.SetAction(AloneModeState.WidgetToggleAction);

                var pendingIntent = PendingIntent.GetBroadcast(
                    context,
                    widgetId,
                    intent,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

                views.SetOnClickPendingIntent(Resource.Id.widgetButton, pendingIntent);
                views.SetTextViewText(Resource.Id.widgetButton, AloneModeState.IsAlone ? "Alone On" : "Alone Off");

                appWidgetManager.UpdateAppWidget(widgetId, views);
            }
        }
    }
}

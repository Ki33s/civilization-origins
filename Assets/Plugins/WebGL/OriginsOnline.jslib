mergeInto(LibraryManager.library, {
  OriginsOnlineReady: function() { if(window.originsReady) window.originsReady(); },
  OriginsInvite: function(code) { if(window.originsInvite) window.originsInvite(UTF8ToString(code)); },
  OriginsOnlineReport: function(message) { if(window.originsReport) window.originsReport(UTF8ToString(message)); }
});

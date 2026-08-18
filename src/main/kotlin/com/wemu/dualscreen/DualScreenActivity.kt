package com.wemu.dualscreen

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import com.thor.core.designsystem.theme.ThorTheme
import com.wemu.session.secondscreen.ModsPage
import com.wemu.session.secondscreen.SecondScreen

/**
 * DualScreen Mods, on its own.
 *
 * The same second-screen pages wemu's console draws, for people whose games are
 * not running inside wemu. A mod opens a socket on the loopback port; this
 * listens on it and draws whatever that game's page is. Nothing about those pages
 * knows which of the two apps is hosting them, which is why the whole of it lives
 * in a library rather than in either app.
 *
 * Not a launcher and not an emulator: it starts no game, manages no container and
 * has no library. That is what makes it worth shipping separately — somebody
 * running Stardew some other way should not need a Steam client and an x86
 * translation layer to see their crops on a second panel.
 */
class DualScreenActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            DualScreenTheme {
                /*
                 * A Surface, not a Box.
                 *
                 * It sets the content colour as well as the background, so text
                 * naming no colour of its own inherits the theme's rather than
                 * Material's default. Inside wemu these pages always sit within
                 * one already; here there is nothing above them.
                 */
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = ThorTheme.colors.background,
                    contentColor = ThorTheme.colors.onSurface,
                ) {
                    Box(modifier = Modifier.fillMaxSize().systemBarsPadding()) {
                        ModsPage(modifier = Modifier.fillMaxSize())
                    }
                }
            }
        }
    }

    /*
     * The socket outlives a rotation but not the app.
     *
     * `SecondScreen` is a singleton with a process-lifetime scope, so leaving it
     * open across configuration changes costs nothing and avoids a reconnect
     * every time the panel turns. Closing it when the activity is actually
     * finishing is what stops a background app holding the port a mod is trying
     * to reach.
     */
    override fun onDestroy() {
        if (isFinishing) SecondScreen.shutdown()
        super.onDestroy()
    }
}

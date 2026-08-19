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

class DualScreenActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            DualScreenTheme {

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

    override fun onDestroy() {
        if (isFinishing) SecondScreen.shutdown()
        super.onDestroy()
    }
}

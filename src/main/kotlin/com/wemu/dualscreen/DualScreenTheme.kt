package com.wemu.dualscreen

import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Color
import com.thor.core.designsystem.theme.LocalThorTheme
import com.thor.core.designsystem.theme.ThorColors
import com.thor.core.designsystem.theme.ThorTheme

@Composable
fun DualScreenTheme(content: @Composable () -> Unit) {
    ThorTheme {
        val base = LocalThorTheme.current
        val themed = remember(base) { base.copy(colors = Paper) }

        CompositionLocalProvider(
            LocalThorTheme provides themed,
            LocalContentColor provides Paper.onSurface,
        ) {
            MaterialTheme(
                colorScheme = MaterialTheme.colorScheme.copy(
                    primary = Paper.primary,
                    onPrimary = Color.White,
                    primaryContainer = Paper.surfaceHighest,
                    onPrimaryContainer = Paper.onSurface,
                    secondary = Paper.secondary,
                    onSecondary = Color.White,
                    tertiary = Paper.primary,
                    onTertiary = Color.White,
                    background = Paper.background,
                    onBackground = Paper.onBackground,
                    surface = Paper.surface,
                    onSurface = Paper.onSurface,
                    surfaceVariant = Paper.surfaceHighest,
                    onSurfaceVariant = Paper.onSurfaceVariant,
                    surfaceContainer = Paper.surfaceElevated,
                    surfaceContainerHigh = Paper.surfaceHighest,
                    surfaceContainerHighest = Paper.surfaceHighest,
                    outline = Paper.outline,
                    outlineVariant = Paper.outline,
                    error = Paper.error,
                    onError = Color.White,
                    scrim = Paper.scrim,
                ),
                typography = MaterialTheme.typography,
                shapes = MaterialTheme.shapes,
                content = content,
            )
        }
    }
}

private val Ink = Color(0xFF1B1714)
private val InkMuted = Color(0xFF5C5044)
private val Parchment = Color(0xFFF6EEDD)
private val Card = Color(0xFFFFFAF0)
private val CardHigh = Color(0xFFEADDC4)
private val Edge = Color(0xFFC9B79A)
private val Amber = Color(0xFFC8791E)
private val AmberDeep = Color(0xFF8A4E12)

private val Paper = ThorColors(
    primary = Amber,
    secondary = AmberDeep,
    accentEnd = AmberDeep,
    background = Parchment,
    surface = Card,
    surfaceElevated = Card,
    surfaceHighest = CardHigh,
    onBackground = Ink,
    onSurface = Ink,
    onSurfaceVariant = InkMuted,
    cursor = Amber,
    glow = Color(0xFFE0A64A),
    outline = Edge,
    error = Color(0xFFA33223),
    scrim = Color(0x99000000),
    tints = listOf(Amber, AmberDeep, Color(0xFF6B8E4E), Color(0xFF4E7A8E), Color(0xFF8E4E6B)),
)

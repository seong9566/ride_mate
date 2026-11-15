package com.example.pickup_hyundai

import android.view.inputmethod.EditorInfo
import android.view.inputmethod.InputConnection
import androidx.compose.ui.ExperimentalComposeUiApi
import androidx.compose.ui.platform.InterceptPlatformTextInput
import androidx.compose.ui.test.assertTextEquals
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.performClick
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.ui.setup.ChildInfoScreen
import com.example.pickup_hyundai.ui.setup.SetupUiState
import kotlinx.coroutines.awaitCancellation
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalComposeUiApi::class)
class ChildNameInputTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun hangulCompositionSurvivesBeforeScreenStateCatchesUp() {
        lateinit var connection: InputConnection
        var draft = ChildInfoInput()
        compose.setContent {
            InterceptPlatformTextInput(interceptor = { request, _ ->
                connection = request.createInputConnection(EditorInfo())
                awaitCancellation()
            }) {
                // Simulate pending ViewModel updates by keeping the original screen state.
                ChildInfoScreen(SetupUiState(), onChange = { draft = it(draft) }, onNext = {})
            }
        }
        val name = compose.onAllNodes(hasSetTextAction())[0]
        name.performClick()

        for (composingText in listOf("ㅈ", "지", "짐", "지미", "지민")) {
            compose.runOnIdle { connection.setComposingText(composingText, 1) }
            name.assertTextEquals(composingText)
        }
        compose.runOnIdle { connection.finishComposingText() }
        name.assertTextEquals("지민")
        compose.runOnIdle { assertEquals("지민", draft.name) }
    }
}

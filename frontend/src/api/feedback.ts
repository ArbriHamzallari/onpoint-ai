import { api } from './client'
import type {
  FeedbackResponse,
  GuestIssueStatus,
  SubmitFeedbackRequest,
} from '../types'

export const feedbackApi = {
  submit: (body: SubmitFeedbackRequest) =>
    api.post<FeedbackResponse>('/api/feedback', body, { skipAuth: true }),

  /**
   * Returns the issue tied to the current guest session (op_session cookie).
   * Throws ApiError(404) when the session has no issue (e.g. positive feedback
   * never created one). Callers should branch on err.status === 404.
   */
  getMyIssue: () =>
    api.get<GuestIssueStatus>('/api/feedback/me/issue', { skipAuth: true }),
}

import { api } from './client'
import type { FeedbackResponse, SubmitFeedbackRequest } from '../types'

export const feedbackApi = {
  submit: (body: SubmitFeedbackRequest) =>
    api.post<FeedbackResponse>('/api/feedback', body, { skipAuth: true }),
}

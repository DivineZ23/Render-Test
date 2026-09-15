import { UserRole } from './user.models';

export interface AuctionCommissionCalculation {
  finalAuctionPrice: number;
  basePrice: number;
  auctionPremium: number;
  additionalAgentPool: number;
  companyShare: number;
  winningAgentBaseShare: number;
  winningAgentClosingShare: number;
  winningAgentTotal: number;
  amountPerWinningAgent: number;
  participationPool: number;
  amountPerOtherAgent: number;
  totalAgentCount: number;
  winningAgentCount: number;
  otherAgentCount: number;
}

export interface CommissionRecord {
  id: string;
  settlementId: string;
  auctionReference: string;
  agentUserId: string;
  agentDisplayName: string;
  agentRole: UserRole;
  isWinningAgent: boolean;
  totalAgentCount: number;
  finalAuctionPrice: number;
  basePrice: number;
  auctionPremium: number;
  additionalAgentPool: number;
  baseShare: number;
  premiumShare: number;
  commissionAmount: number;
  isPaid: boolean;
  paidAt?: string;
  paidByUserId?: string;
  paidByDisplayName?: string;
  createdAt: string;
}

export interface AgentCommissionSummary {
  userId: string;
  displayName: string;
  role: UserRole;
  totalCommission: number;
  outstandingCommission: number;
  receivedCommission: number;
  auctionCount: number;
  outstandingCount: number;
}

export interface CommissionOverview {
  agents: AgentCommissionSummary[];
  records: CommissionRecord[];
  totalOutstanding: number;
  totalReceived: number;
}

export interface CreateAuctionSettlement {
  auctionReference: string;
  finalAuctionPrice: number;
  basePrice: number;
  winningAgentUserIds: string[];
  otherAgentUserIds: string[];
}
